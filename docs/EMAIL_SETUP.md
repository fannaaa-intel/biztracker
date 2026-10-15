# E-mail setup (Forgot Password)

BizTracker e-mails a 6-digit code when someone clicks **"Forgot your password?"** on the sign-in screen.
It sends through **Gmail** (`smtp.gmail.com`, port 587, SSL) using a **Gmail App Password**.
The address and App Password are stored in a local file, **`email-settings.json`**. That file is in `.gitignore`
and must **never** be committed or shared.

You do this once per computer that runs BizTracker.

---

## 1. Choose the Gmail account that sends the codes
Use a Gmail account made for the office, for example `biztracker.aparri@gmail.com`, not a personal one.
Users will see this address as the sender.

## 2. Turn on 2-Step Verification
App Passwords only exist when 2-Step Verification is on.

1. Sign in to that Gmail account and open <https://myaccount.google.com/security>.
2. Under **"How you sign in to Google"**, click **2-Step Verification** and follow the steps (phone number or authenticator app).

## 3. Create an App Password
1. Open <https://myaccount.google.com/apppasswords> (sign in again if asked).
2. Under **App name**, type `BizTracker`, then click **Create**.
3. Google shows a **16-letter password** such as `abcd efgh ijkl mnop`. **Copy it now.** Google shows it only once.
   (The spaces don't matter; BizTracker removes them.)

> If the page says *"The setting you are looking for is not available for your account"*, 2-Step Verification is
> still off, or the account is a school/work account whose admin blocks App Passwords. Use a normal Gmail account.

## 4. Create `email-settings.json`
1. In the project, go to `src/BizTracker/`.
2. Copy **`email-settings.example.json`** and rename the copy to **`email-settings.json`** (same folder).
3. Open it in Notepad and replace the placeholders:

```json
{
  "GmailAddress": "biztracker.aparri@gmail.com",
  "AppPassword": "abcd efgh ijkl mnop",
  "SenderName": "BizTracker - Municipality of Aparri"
}
```

4. Save. Then check that Git ignores it. In the project folder, `git status` must **not** list `email-settings.json`.

## 5. Build and run
```
dotnet build
dotnet run --project src/BizTracker
```
The build copies `email-settings.json` next to `BizTracker.exe` (`src/BizTracker/bin/Debug/net8.0-windows/`).
If you copy the finished app to another PC, put `email-settings.json` next to `BizTracker.exe` there as well.
After editing the file, rebuild (or edit the copy next to the .exe).

## 6. Test it
1. Sign in as **admin**. Go to **Settings > Users**, pick an account, click **Edit User**, and set its **E-mail** to an inbox you can open.
   (The seeded accounts use `@example.com` addresses on purpose, so test e-mails never reach a real person.)
2. Sign out. Click **Forgot your password?**, type that username, and click **Send Code**.
3. The e-mail "Your BizTracker password reset code" should arrive within a minute. Check **Spam** too.

---

## Troubleshooting
| Message in BizTracker | What to do |
|---|---|
| "E-mail is not set up on this computer yet" | `email-settings.json` is missing from the .exe folder, still has the `PUT-...` placeholders, the App Password is not 16 letters, or the JSON is broken (check the quotes and commas). |
| "No internet connection" | The PC is offline, or it cannot reach `smtp.gmail.com`. |
| "Gmail did not accept the sending account" | The Gmail address or App Password is wrong. Create a new App Password (step 3). |
| "Gmail could not send the e-mail right now" | Try again later. A firewall or antivirus may block port **587**. A Gmail address that does not exist also gives this message. |
| No e-mail arrives, but no error | Check Spam. Make sure the account has the right e-mail (Settings > Users). Inactive accounts and accounts without an e-mail never get a code (on purpose, the screen does not say so). |

## Security notes
- Never put the App Password in `App.config`, in code, or in any committed file.
- If the App Password leaks, delete it at <https://myaccount.google.com/apppasswords> and create a new one.
- Codes are stored only as BCrypt hashes in `password_resets`, expire after 10 minutes, work once, and are cancelled after 5 wrong tries.
