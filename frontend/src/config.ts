// Public Google OAuth client id (safe to expose). Overridable via VITE_GOOGLE_CLIENT_ID.
export const GOOGLE_CLIENT_ID =
  import.meta.env.VITE_GOOGLE_CLIENT_ID ??
  '1034584934194-dg6lhu4jivhootnd98fms24dc4hskf3p.apps.googleusercontent.com'

// PrimeUI community/dev license (offline verification; safe in the client bundle).
export const PRIMEUI_LICENSE =
  import.meta.env.VITE_PRIMEUI_LICENSE ??
  'eyJpZCI6IjBlYWYwMTE3LTNmYzAtNGZlZi05NDdjLTI2ZWUxNWQwOTZjYyIsInByb2R1Y3QiOiJwcmltZXVpIiwidGllciI6ImNvbW11bml0eSIsInR5cGUiOiJkZXYiLCJpYXQiOjE3ODQ1MDk4NTgsImV4cCI6MTgxNjA0NTg1OH0.XiXwbVQz-8fGFGv0W_d6R76uKkwzs50ytf6zXto1BAOmjlvvH2yTaiD1poAkz3zMBst0nxwIcvidjKYNWiiDAw'
