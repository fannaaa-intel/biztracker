''' <summary>
''' Database access for the users table.
''' Users are never deleted - they are deactivated (is_active = 0) so the audit log stays valid.
''' </summary>
Public NotInheritable Class UserRepository

    Private Sub New()
    End Sub

    Private Const SelectSql As String = "SELECT * FROM users "

    Public Shared Function GetAll() As List(Of User)
        Return ToList(Db.GetDataTable(SelectSql & "ORDER BY role, username"))
    End Function

    Public Shared Function GetById(userId As Integer) As User
        Return ToList(Db.GetDataTable(SelectSql & "WHERE user_id = @id", Db.P("@id", userId))).FirstOrDefault()
    End Function

    ''' <summary>Used by login. Returns Nothing if the username does not exist.</summary>
    Public Shared Function GetByUsername(username As String) As User
        Return ToList(Db.GetDataTable(SelectSql & "WHERE username = @username",
                                      Db.P("@username", username.Trim()))).FirstOrDefault()
    End Function

    ''' <summary>Owner accounts linked to a business.</summary>
    Public Shared Function GetByBusinessId(businessId As Integer) As List(Of User)
        Return ToList(Db.GetDataTable(SelectSql & "WHERE business_id = @bid ORDER BY username",
                                      Db.P("@bid", businessId)))
    End Function

    ''' <summary>Inserts a user (PasswordHash must already be a BCrypt hash). Returns new user_id or -1.</summary>
    Public Shared Function Insert(u As User) As Integer
        Const sql As String =
            "INSERT INTO users (username, password_hash, full_name, role, business_id, is_active) " &
            "VALUES (@username, @hash, @name, @role, @bid, @active)"
        Return Db.ExecuteInsert(sql,
            Db.P("@username", u.Username.Trim()),
            Db.P("@hash", u.PasswordHash),
            Db.P("@name", u.FullName.Trim()),
            Db.P("@role", u.Role),
            Db.P("@bid", u.BusinessId),
            Db.P("@active", u.IsActive))
    End Function

    ''' <summary>Updates name, role, linked business and active flag (not the password).</summary>
    Public Shared Function Update(u As User) As Boolean
        Const sql As String =
            "UPDATE users SET full_name = @name, role = @role, business_id = @bid, is_active = @active " &
            "WHERE user_id = @id"
        Return Db.ExecuteNonQuery(sql,
            Db.P("@name", u.FullName.Trim()),
            Db.P("@role", u.Role),
            Db.P("@bid", u.BusinessId),
            Db.P("@active", u.IsActive),
            Db.P("@id", u.UserId)) > 0
    End Function

    Public Shared Function UpdatePassword(userId As Integer, passwordHash As String) As Boolean
        Return Db.ExecuteNonQuery("UPDATE users SET password_hash = @hash WHERE user_id = @id",
                                  Db.P("@hash", passwordHash), Db.P("@id", userId)) > 0
    End Function

    Public Shared Function SetActive(userId As Integer, isActive As Boolean) As Boolean
        Return Db.ExecuteNonQuery("UPDATE users SET is_active = @active WHERE user_id = @id",
                                  Db.P("@active", isActive), Db.P("@id", userId)) > 0
    End Function

    Public Shared Function UpdateLastLogin(userId As Integer) As Boolean
        Return Db.ExecuteNonQuery("UPDATE users SET last_login = NOW() WHERE user_id = @id",
                                  Db.P("@id", userId)) > 0
    End Function

    ' ---------------- helpers ----------------

    Private Shared Function ToList(table As DataTable) As List(Of User)
        Return table.Rows.Cast(Of DataRow)().Select(AddressOf Map).ToList()
    End Function

    Private Shared Function Map(row As DataRow) As User
        Return New User With {
            .UserId = row.GetInt("user_id"),
            .Username = row.GetString("username"),
            .PasswordHash = row.GetString("password_hash"),
            .FullName = row.GetString("full_name"),
            .Role = row.GetString("role"),
            .BusinessId = row.GetNullableInt("business_id"),
            .IsActive = row.GetBool("is_active"),
            .LastLogin = row.GetNullableDate("last_login"),
            .CreatedAt = row.GetDate("created_at")
        }
    End Function

End Class
