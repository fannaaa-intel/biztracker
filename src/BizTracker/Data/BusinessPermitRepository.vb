Imports MySql.Data.MySqlClient

''' <summary>
''' Database access for business_permits and their clearance_endorsements.
''' A new permit always gets 5 Pending endorsements (Barangay, Sanitary, RPT, Fire, Zoning).
''' </summary>
Public NotInheritable Class BusinessPermitRepository

    Private Sub New()
    End Sub

    Private Const SelectSql As String =
        "SELECT bp.*, b.business_name FROM business_permits bp " &
        "JOIN businesses b ON b.business_id = bp.business_id "

    Private Const OrderSql As String = " ORDER BY bp.permit_year DESC, bp.date_filed DESC, bp.permit_id DESC"

    ' ==================== Permits ====================

    Public Shared Function GetAll() As List(Of BusinessPermit)
        Return ToList(Db.GetDataTable(SelectSql & "WHERE b.is_active = 1" & OrderSql))
    End Function

    Public Shared Function GetById(permitId As Integer) As BusinessPermit
        Return ToList(Db.GetDataTable(SelectSql & "WHERE bp.permit_id = @id", Db.P("@id", permitId))).FirstOrDefault()
    End Function

    ''' <summary>All permits of one business, newest year first.</summary>
    Public Shared Function GetByBusinessId(businessId As Integer) As List(Of BusinessPermit)
        Return ToList(Db.GetDataTable(SelectSql & "WHERE bp.business_id = @bid" & OrderSql, Db.P("@bid", businessId)))
    End Function

    ''' <summary>
    ''' Inserts a permit AND its 5 Pending endorsements in one transaction.
    ''' Returns the new permit_id, or -1 on error.
    ''' </summary>
    Public Shared Function Insert(p As BusinessPermit) As Integer
        Const permitSql As String =
            "INSERT INTO business_permits (business_id, reference_no, application_type, permit_year, status, " &
            "  gross_receipts, assessed_amount, surcharge, interest, mayors_permit_no, date_filed, " &
            "  date_issued, valid_until, remarks) " &
            "VALUES (@bid, @ref, @type, @year, @status, @gross, @assessed, @surcharge, @interest, @mp, " &
            "  @filed, @issued, @valid, @remarks)"
        Const endorseSql As String =
            "INSERT INTO clearance_endorsements (permit_id, office, status) VALUES (@pid, @office, 'Pending')"

        Dim newId As Integer = -1
        Dim ok = Db.RunInTransaction(
            Sub(conn, tx)
                newId = Db.TxExecute(conn, tx, permitSql, ToParameters(p).Concat({
                                         Db.P("@bid", p.BusinessId),
                                         Db.P("@ref", p.ReferenceNo)}).ToArray())
                For Each office In EndorsementOffices.All
                    Db.TxExecute(conn, tx, endorseSql, Db.P("@pid", newId), Db.P("@office", office))
                Next
            End Sub)
        Return If(ok, newId, -1)
    End Function

    ''' <summary>Updates the editable fields. Status is changed with UpdateStatus().</summary>
    Public Shared Function Update(p As BusinessPermit) As Boolean
        Const sql As String =
            "UPDATE business_permits SET application_type = @type, permit_year = @year, status = @status, " &
            "  gross_receipts = @gross, assessed_amount = @assessed, surcharge = @surcharge, " &
            "  interest = @interest, mayors_permit_no = @mp, date_filed = @filed, date_issued = @issued, " &
            "  valid_until = @valid, remarks = @remarks " &
            "WHERE permit_id = @id"
        Return Db.ExecuteNonQuery(sql, ToParameters(p).Append(Db.P("@id", p.PermitId)).ToArray()) > 0
    End Function

    Public Shared Function UpdateStatus(permitId As Integer, newStatus As String) As Boolean
        Return Db.ExecuteNonQuery("UPDATE business_permits SET status = @status WHERE permit_id = @id",
                                  Db.P("@status", newStatus), Db.P("@id", permitId)) > 0
    End Function

    ''' <summary>
    ''' Hard delete - only allowed while the application is still "Submitted".
    ''' Also removes its endorsements and requirement rows (same transaction).
    ''' </summary>
    Public Shared Function Delete(permitId As Integer) As Boolean
        Return Db.RunInTransaction(
            Sub(conn, tx)
                Dim status = Db.TxScalar(conn, tx,
                    "SELECT status FROM business_permits WHERE permit_id = @id FOR UPDATE", Db.P("@id", permitId))
                If status Is Nothing Then Throw New BusinessRuleException("The application no longer exists.")
                If status.ToString() <> "Submitted" Then
                    Throw New BusinessRuleException("Only applications with status 'Submitted' can be deleted.")
                End If
                Db.TxExecute(conn, tx, "DELETE FROM clearance_endorsements WHERE permit_id = @id", Db.P("@id", permitId))
                Db.TxExecute(conn, tx, "DELETE FROM requirements WHERE module = @module AND related_id = @id",
                             Db.P("@module", ModuleNames.BusinessPermit), Db.P("@id", permitId))
                Db.TxExecute(conn, tx, "DELETE FROM business_permits WHERE permit_id = @id", Db.P("@id", permitId))
            End Sub)
    End Function

    ' ==================== Endorsements ====================

    Public Shared Function GetEndorsements(permitId As Integer) As List(Of ClearanceEndorsement)
        Const sql As String =
            "SELECT ce.*, u.full_name AS endorsed_by_name FROM clearance_endorsements ce " &
            "LEFT JOIN users u ON u.user_id = ce.endorsed_by " &
            "WHERE ce.permit_id = @pid " &
            "ORDER BY FIELD(ce.office, 'Barangay', 'Sanitary', 'RPT', 'Fire', 'Zoning')"
        Dim table = Db.GetDataTable(sql, Db.P("@pid", permitId))
        Return table.Rows.Cast(Of DataRow)().Select(
            Function(row) New ClearanceEndorsement With {
                .EndorsementId = row.GetInt("endorsement_id"),
                .PermitId = row.GetInt("permit_id"),
                .Office = row.GetString("office"),
                .Status = row.GetString("status"),
                .EndorsedBy = row.GetNullableInt("endorsed_by"),
                .EndorsedAt = row.GetNullableDate("endorsed_at"),
                .Remarks = row.GetString("remarks"),
                .EndorsedByName = row.GetString("endorsed_by_name")
            }).ToList()
    End Function

    ''' <summary>
    ''' Sets one office's endorsement (Endorsed / Rejected / Pending) for a permit.
    ''' Used by the endorsement panel and by other modules (e.g. issuing a sanitary
    ''' permit endorses "Sanitary"). Pending clears who/when.
    ''' </summary>
    Public Shared Function SetEndorsement(permitId As Integer, office As String, status As String,
                                          userId As Integer?, remarks As String) As Boolean
        Const sql As String =
            "UPDATE clearance_endorsements SET status = @status, " &
            "  endorsed_by = IF(@status = 'Pending', NULL, @uid), " &
            "  endorsed_at = IF(@status = 'Pending', NULL, NOW()), " &
            "  remarks = @remarks " &
            "WHERE permit_id = @pid AND office = @office"
        Return Db.ExecuteNonQuery(sql,
            Db.P("@status", status),
            Db.P("@uid", userId),
            Db.P("@remarks", Db.NullIfEmpty(remarks)),
            Db.P("@pid", permitId),
            Db.P("@office", office)) > 0
    End Function

    ' ---------------- helpers ----------------

    ''' <summary>Parameters shared by Insert and Update (not business_id / reference_no / id).</summary>
    Private Shared Function ToParameters(p As BusinessPermit) As MySqlParameter()
        Return {
            Db.P("@type", p.ApplicationType),
            Db.P("@year", p.PermitYear),
            Db.P("@status", p.Status),
            Db.P("@gross", p.GrossReceipts),
            Db.P("@assessed", p.AssessedAmount),
            Db.P("@surcharge", p.Surcharge),
            Db.P("@interest", p.Interest),
            Db.P("@mp", Db.NullIfEmpty(p.MayorsPermitNo)),
            Db.P("@filed", p.DateFiled.Date),
            Db.P("@issued", p.DateIssued),
            Db.P("@valid", p.ValidUntil),
            Db.P("@remarks", Db.NullIfEmpty(p.Remarks))
        }
    End Function

    Private Shared Function ToList(table As DataTable) As List(Of BusinessPermit)
        Return table.Rows.Cast(Of DataRow)().Select(AddressOf Map).ToList()
    End Function

    Private Shared Function Map(row As DataRow) As BusinessPermit
        Return New BusinessPermit With {
            .PermitId = row.GetInt("permit_id"),
            .BusinessId = row.GetInt("business_id"),
            .ReferenceNo = row.GetString("reference_no"),
            .ApplicationType = row.GetString("application_type"),
            .PermitYear = row.GetInt("permit_year"),
            .Status = row.GetString("status"),
            .GrossReceipts = row.GetDecimal("gross_receipts"),
            .AssessedAmount = row.GetDecimal("assessed_amount"),
            .Surcharge = row.GetDecimal("surcharge"),
            .Interest = row.GetDecimal("interest"),
            .MayorsPermitNo = row.GetString("mayors_permit_no"),
            .DateFiled = row.GetDate("date_filed"),
            .DateIssued = row.GetNullableDate("date_issued"),
            .ValidUntil = row.GetNullableDate("valid_until"),
            .Remarks = row.GetString("remarks"),
            .CreatedAt = row.GetDate("created_at"),
            .BusinessName = row.GetString("business_name")
        }
    End Function

End Class
