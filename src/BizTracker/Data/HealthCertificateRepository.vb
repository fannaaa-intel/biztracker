''' <summary>
''' Database access for the health_certificates table.
''' An employee can have many certificates over the years; the "current" one is the
''' one with the latest expiry date. Status is computed by StatusService, not stored.
''' </summary>
Public NotInheritable Class HealthCertificateRepository

    Private Sub New()
    End Sub

    Private Const SelectSql As String = "SELECT hc.* FROM health_certificates hc "

    Public Shared Function GetAll() As List(Of HealthCertificate)
        Return ToList(Db.GetDataTable(SelectSql & "ORDER BY hc.expiry_date"))
    End Function

    Public Shared Function GetById(certId As Integer) As HealthCertificate
        Return ToList(Db.GetDataTable(SelectSql & "WHERE hc.cert_id = @id", Db.P("@id", certId))).FirstOrDefault()
    End Function

    ''' <summary>Certificate history of one employee, newest first.</summary>
    Public Shared Function GetByEmployeeId(employeeId As Integer) As List(Of HealthCertificate)
        Return ToList(Db.GetDataTable(SelectSql & "WHERE hc.employee_id = @eid ORDER BY hc.expiry_date DESC",
                                      Db.P("@eid", employeeId)))
    End Function

    ''' <summary>
    ''' The CURRENT certificate (latest expiry) of every ACTIVE employee of a business.
    ''' Employees with no certificate yet are not included.
    ''' </summary>
    Public Shared Function GetLatestByBusinessId(businessId As Integer) As List(Of HealthCertificate)
        Const sql As String =
            SelectSql &
            "JOIN employees e ON e.employee_id = hc.employee_id " &
            "WHERE e.business_id = @bid AND e.is_active = 1 " &
            "  AND hc.cert_id = (SELECT h2.cert_id FROM health_certificates h2 " &
            "                    WHERE h2.employee_id = hc.employee_id " &
            "                    ORDER BY h2.expiry_date DESC, h2.cert_id DESC LIMIT 1) " &
            "ORDER BY hc.expiry_date"
        Return ToList(Db.GetDataTable(sql, Db.P("@bid", businessId)))
    End Function

    ''' <summary>Returns the new cert_id, or -1 on error.</summary>
    Public Shared Function Insert(c As HealthCertificate) As Integer
        Const sql As String =
            "INSERT INTO health_certificates (employee_id, certificate_no, issue_date, expiry_date, issued_by) " &
            "VALUES (@eid, @no, @issue, @expiry, @by)"
        Return Db.ExecuteInsert(sql,
            Db.P("@eid", c.EmployeeId),
            Db.P("@no", c.CertificateNo),
            Db.P("@issue", c.IssueDate.Date),
            Db.P("@expiry", c.ExpiryDate.Date),
            Db.P("@by", Db.NullIfEmpty(c.IssuedBy)))
    End Function

    ''' <summary>
    ''' Inserts several certificates as ONE transaction (all or nothing) and fills in each CertId.
    ''' Used by "Renew Selected".
    ''' </summary>
    Public Shared Function InsertMany(certs As List(Of HealthCertificate)) As Boolean
        Const sql As String =
            "INSERT INTO health_certificates (employee_id, certificate_no, issue_date, expiry_date, issued_by) " &
            "VALUES (@eid, @no, @issue, @expiry, @by)"
        Dim newIds As New List(Of Integer)
        Dim ok = Db.RunInTransaction(
            Sub(conn, tx)
                For Each c In certs
                    newIds.Add(Db.TxExecute(conn, tx, sql,
                        Db.P("@eid", c.EmployeeId),
                        Db.P("@no", c.CertificateNo),
                        Db.P("@issue", c.IssueDate.Date),
                        Db.P("@expiry", c.ExpiryDate.Date),
                        Db.P("@by", Db.NullIfEmpty(c.IssuedBy))))
                Next
            End Sub)
        If ok Then
            For i = 0 To certs.Count - 1
                certs(i).CertId = newIds(i)
            Next
        End If
        Return ok
    End Function

    Public Shared Function Update(c As HealthCertificate) As Boolean
        Const sql As String =
            "UPDATE health_certificates SET issue_date = @issue, expiry_date = @expiry, issued_by = @by " &
            "WHERE cert_id = @id"
        Return Db.ExecuteNonQuery(sql,
            Db.P("@issue", c.IssueDate.Date),
            Db.P("@expiry", c.ExpiryDate.Date),
            Db.P("@by", Db.NullIfEmpty(c.IssuedBy)),
            Db.P("@id", c.CertId)) > 0
    End Function

    ' ---------------- helpers ----------------

    Private Shared Function ToList(table As DataTable) As List(Of HealthCertificate)
        Return table.Rows.Cast(Of DataRow)().Select(AddressOf Map).ToList()
    End Function

    Private Shared Function Map(row As DataRow) As HealthCertificate
        Return New HealthCertificate With {
            .CertId = row.GetInt("cert_id"),
            .EmployeeId = row.GetInt("employee_id"),
            .CertificateNo = row.GetString("certificate_no"),
            .IssueDate = row.GetDate("issue_date"),
            .ExpiryDate = row.GetDate("expiry_date"),
            .IssuedBy = row.GetString("issued_by"),
            .CreatedAt = row.GetDate("created_at")
        }
    End Function

End Class
