import api from './api';

/**
 * Log in a user with email and password.
 * Returns { token, user } on success.
 * Throws an Error with a user-friendly message on failure.
 */
export async function loginUser(email, password) {
  try {
    // FIXED: the URL was '/auth/login'. There is no AuthController in the API —
    // the route is api/Users/login (UsersController + [HttpPost("login")]),
    // so every login call was coming back as 404.
    // FIXED: the C# LoginRequest class has properties named Email and Password
    // (capital letters). ASP.NET's JSON binding is case-insensitive so lowercase
    // still binds, but they are spelled out here so it is obvious what the API wants.
    const response = await api.post('/Users/login', { Email: email, Password: password });

    // NOTE: the API returns a FLAT object: { token, usr_Id, usr_FullName, usr_Role }
    // — not { token, user }. Whatever calls this function should read
    // response.usr_Role, not response.user.role.
    return response.data;
  } catch (error) {
    throw new Error(
      // ADDED: the API sends plain strings for errors (e.g. "Invalid email or password"),
      // not { message: ... }. Checking both means the real message reaches the user
      // instead of the generic fallback.
      (typeof error.response?.data === 'string' ? error.response.data : null) ||
        error.response?.data?.message ||
        'Login failed. Please try again.'
    );
  }
}

/**
 * Register a new user account.
 * Returns { token, user } on success.
 * Throws an Error with a user-friendly message on failure.
 */
// FIXED: the parameter list did not match the backend at all.
// UsersController.Register binds a `User` object and reads:
//   usr_FullName, usr_Email, usr_PasswordHash, usr_NationalId
// It ignores anything else and ALWAYS sets the role to "Resident" server-side,
// so sending `role` or `municipality` from the browser did nothing (which is
// correct security-wise — a user must not be able to make themselves Admin).
// Staff and Admin accounts have to be created by the Seeder or by an admin tool.
export async function registerUser(fullName, email, password, nationalId) {
  try {
    // FIXED: was '/auth/register' → 404. Correct route is api/Users/register.
    const response = await api.post('/Users/register', {
      usr_FullName: fullName,
      usr_Email: email,
      usr_PasswordHash: password, // the API hashes this with BCrypt before saving
      usr_NationalId: nationalId,
    });

    // NOTE: returns { token, usr_Id, usr_FullName, usr_Role } — flat, same as login.
    return response.data;
  } catch (error) {
    throw new Error(
      // ADDED: same string-vs-object error handling as loginUser above
      (typeof error.response?.data === 'string' ? error.response.data : null) ||
        error.response?.data?.message ||
        'Registration failed. Please try again.'
    );
  }
}
