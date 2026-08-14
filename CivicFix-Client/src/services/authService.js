import api from './api';

/**
 * Log in a user with email and password.
 * Returns { token, user } on success.
 * Throws an Error with a user-friendly message on failure.
 */
export async function loginUser(email, password) {
  try {
    const response = await api.post('/auth/login', { email, password });
    return response.data; // expects { token, user }
  } catch (error) {
    throw new Error(
      error.response?.data?.message || 'Login failed. Please try again.'
    );
  }
}

/**
 * Register a new user account.
 * Returns { token, user } on success.
 * Throws an Error with a user-friendly message on failure.
 */
export async function registerUser(firstName, lastName, email, password, municipality, role) {
  try {
    const response = await api.post('/auth/register', {
      firstName,
      lastName,
      email,
      password,
      municipality,
      role,
    });
    return response.data; // expects { token, user }
  } catch (error) {
    throw new Error(
      error.response?.data?.message || 'Registration failed. Please try again.'
    );
  }
}
