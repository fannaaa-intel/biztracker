''' <summary>
''' Database access for the password_resets table (Forgot Password codes).
''' All times use the database server's clock (NOW()), so a wrong PC clock cannot extend a code.
''' Used only by PasswordResetService.
''' </summary>
Public NotInheritable Class PasswordResetRepository

    Private Sub New()
    End Sub

    Private Const SelectSql As String =
        "SELECT r.*, (r.expires_at <= NOW()) AS is_expired FROM password_resets r "

    ''' <summary>The user's newest code (used, expired or still open), or Nothing.</summary>
    Public Shared Function GetLatest(userId As Integer) As PasswordReset
        Return ToList(Db.GetDataTable(SelectSql & "WHERE r.user_id = @uid ORDER BY r.created_at DESC, r.reset_id DESC LIMIT 1",
                                      Db.P("@uid", userId))).FirstOrDefault()
    End Function

    ''' <summary>True if the user was sent a code less than "seconds" ago (resend cooldown).</summary>
    Public Shared Function HasRecentCode(userId As Integer, seconds As Integer) As Boolean
        Return Convert.ToInt32(Db.ExecuteScalar(
            "SELECT COUNT(*) FROM password_resets WHERE user_id = @uid AND created_at > NOW() - INTERVAL @sec SECOND",
            Db.P("@uid", userId), Db.P("@sec", seconds))) > 0
    End Function

    ''' <summary>
    ''' Invalidates the user's older unused codes and saves the new one, in one transaction.
    ''' Returns the new reset_id, or -1 on error.
    ''' </summary>
    Public Shared Function InsertReplacingOld(userId As Integer, codeHash As String, validMinutes As Integer) As Integer
        Dim newId = -1
        Dim ok = Db.RunInTransaction(
            Sub(conn, tx)
                Db.TxExecute(conn, tx, "UPDATE password_resets SET used_at = NOW() WHERE user_id = @uid AND used_at IS NULL",
                             Db.P("@uid", userId))
                newId = Db.TxExecute(conn, tx,
                    "INSERT INTO password_resets (user_id, code_hash, expires_at) " &
                    "VALUES (@uid, @hash, NOW() + INTERVAL @min MINUTE)",
                    Db.P("@uid", userId), Db.P("@hash", codeHash), Db.P("@min", validMinutes))
            End Sub)
        Return If(ok, newId, -1)
    End Function

    ''' <summary>Adds one wrong guess. Returns the new attempt count (or -1 on error).</summary>
    Public Shared Function AddAttempt(resetId As Integer) As Integer
        If Db.ExecuteNonQuery("UPDATE password_resets SET attempts = attempts + 1 WHERE reset_id = @id", Db.P("@id", resetId)) <= 0 Then
            Return -1
        End If
        Return Convert.ToInt32(Db.ExecuteScalar("SELECT attempts FROM password_resets WHERE reset_id = @id", Db.P("@id", resetId)))
    End Function

    ''' <summary>Marks a code as used or invalidated (it can never be used again).</summary>
    Public Shared Function Close(resetId As Integer) As Boolean
        Return Db.ExecuteNonQuery("UPDATE password_resets SET used_at = NOW() WHERE reset_id = @id AND used_at IS NULL",
                                  Db.P("@id", resetId)) > 0
    End Function

    ''' <summary>Removes a code whose e-mail could not be sent (so it does not start the resend cooldown).</summary>
    Public Shared Function Delete(resetId As Integer) As Boolean
        Return Db.ExecuteNonQuery("DELETE FROM password_resets WHERE reset_id = @id", Db.P("@id", resetId)) > 0
    End Function

    ''' <summary>
    ''' Saves the new password hash and closes the code in ONE transaction.
    ''' Fails (returns False) if the code was used or invalidated in the meantime.
    ''' </summary>
    Public Shared Function CompleteReset(resetId As Integer, userId As Integer, passwordHash As String) As Boolean
        Dim stillOpen = False
        Dim ok = Db.RunInTransaction(
            Sub(conn, tx)
                ' FOR UPDATE locks the code row, so two windows cannot use the same code at once
                stillOpen = Convert.ToInt32(Db.TxScalar(conn, tx,
                    "SELECT COUNT(*) FROM password_resets WHERE reset_id = @id AND user_id = @uid " &
                    "AND used_at IS NULL AND expires_at > NOW() FOR UPDATE",
                    Db.P("@id", resetId), Db.P("@uid", userId))) > 0
                If Not stillOpen Then Return          ' nothing is changed
                Db.TxExecute(conn, tx, "UPDATE password_resets SET used_at = NOW() WHERE reset_id = @id", Db.P("@id", resetId))
                Db.TxExecute(conn, tx, "UPDATE users SET password_hash = @hash WHERE user_id = @uid",
                             Db.P("@hash", passwordHash), Db.P("@uid", userId))
            End Sub)
        Return ok AndAlso stillOpen
    End Function

    ' ---------------- helpers ----------------

    Private Shared Function ToList(table As DataTable) As List(Of PasswordReset)
        Return table.Rows.Cast(Of DataRow)().Select(
            Function(row) New PasswordReset With {
                .ResetId = row.GetInt("reset_id"),
                .UserId = row.GetInt("user_id"),
                .CodeHash = row.GetString("code_hash"),
                .ExpiresAt = row.GetDate("expires_at"),
                .Attempts = row.GetInt("attempts"),
                .CreatedAt = row.GetDate("created_at"),
                .UsedAt = row.GetNullableDate("used_at"),
                .IsExpired = row.GetBool("is_expired")
            }).ToList()
    End Function

End Class
