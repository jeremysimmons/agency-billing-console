Then configure the authentication client in the Google Cloud Console:

## Branding

Open Google Auth Platform → Branding and set:

- App name
- User-support email
- Developer contact email
- Homepage, privacy policy and terms URLs when appropriate
- Audience

Choose:

- Internal when only users from your Google Workspace organization may sign in
- External when users with other Google accounts may sign in

Google’s current interface separates OAuth configuration into Branding, Audience, Data Access, and Clients.

## Client

Create a Web application client and enter values such as:

Authorized JavaScript origins:

- http://localhost:3000
- https://app.example.com

Authorized redirect URIs:

- http://localhost:3000/auth/google/callback
- https://app.example.com/auth/google/callback

Google Sign-In requires the OAuth client ID. Server-side authorization-code flows also use the generated client secret.

After downloading the credentials, your application configuration will typically look like:

```
GOOGLE_CLIENT_ID=1234567890-example.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=your-client-secret
GOOGLE_REDIRECT_URI=https://app.example.com/auth/google/callback
```

Do not commit the client secret to Git. Store it in your deployment platform’s secret manager or Google Secret Manager.