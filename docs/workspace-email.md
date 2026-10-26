# Email confirmation and workspace invitations

Every registered account owns an organization. Workspace invitations are private, expiring links, accepted only by the account with the invited, confirmed email address. Owners and administrators create a link in **Members** and send it to the intended recipient themselves. Creating a link does not send email automatically.

The recipient registers their own organization or signs into their existing account, confirms their email, and accepts the invitation. Their home organization does not change.

## Configure verification and password reset email

Set the following through deployment secrets/environment variables or .NET user secrets. Do not commit SMTP credentials.

| Setting | Meaning |
| --- | --- |
| `Email__Smtp__Host` | SMTP server hostname |
| `Email__Smtp__Port` | SMTP submission port; defaults to `587` |
| `Email__Smtp__From` | Authorized sender email address |
| `Email__Smtp__UserName` | SMTP username, when authentication is required |
| `Email__Smtp__Password` | SMTP password, supplied as a secret |
| `Email__Smtp__EnableSsl` | Enable STARTTLS; defaults to `true` |

The built-in sender uses SMTP submission with STARTTLS, not implicit TLS on port 465. The server must support the configured authentication method. Use an authorized sender and the public application URL for confirmation links.

Without SMTP, local registration and login still work, but an unverified account cannot accept shared-workspace invitations. The confirmation page explicitly reports missing email configuration. Confirmation/reset links are never displayed on a public page or logged. An SMTP delivery error after registration does not undo the newly created account; retry through **Resend email confirmation**.

The invitation return URL is preserved through registration, login and email confirmation. After confirming the email, select **Continue** to return to the invitation, then explicitly accept it.
