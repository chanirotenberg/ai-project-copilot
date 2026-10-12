import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiError } from '../api/types';
import { useAuth } from '../auth/useAuth';

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const GENERIC_LOGIN_ERROR = 'Unable to sign in. Please try again.';

export function LoginPage() {
  const { login, isBootstrapping } = useAuth();
  const navigate = useNavigate();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const trimmedEmail = email.trim();

    if (!trimmedEmail || !password) {
      setSubmitError(null);
      setValidationError('Email and password are required.');
      return;
    }

    if (!EMAIL_PATTERN.test(trimmedEmail)) {
      setSubmitError(null);
      setValidationError('Enter a valid email address.');
      return;
    }

    setValidationError(null);
    setSubmitError(null);
    setIsSubmitting(true);

    try {
      await login(trimmedEmail, password);
      navigate('/', { replace: true });
    } catch (error) {
      // Reuse the backend's generic failure message (it is already
      // deliberately non-specific). Never fabricate a more detailed
      // message that could leak whether an email exists.
      setSubmitError(error instanceof ApiError ? error.message : GENERIC_LOGIN_ERROR);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="page-center">
      <div className="card auth-card">
        <h1 className="card-title">Sign in</h1>
        <form onSubmit={handleSubmit} noValidate>
          <div className="form-field">
            <label htmlFor="login-email">Email</label>
            <input
              id="login-email"
              name="email"
              type="email"
              autoComplete="username"
              value={email}
              disabled={isSubmitting || isBootstrapping}
              onChange={(event) => setEmail(event.target.value)}
            />
          </div>
          <div className="form-field">
            <label htmlFor="login-password">Password</label>
            <input
              id="login-password"
              name="password"
              type="password"
              autoComplete="current-password"
              value={password}
              disabled={isSubmitting || isBootstrapping}
              onChange={(event) => setPassword(event.target.value)}
            />
          </div>

          {validationError ? (
            <p className="alert" role="alert">
              {validationError}
            </p>
          ) : null}
          {submitError ? (
            <p className="alert" role="alert">
              {submitError}
            </p>
          ) : null}

          <button
            type="submit"
            className="btn btn-primary"
            disabled={isSubmitting || isBootstrapping}
          >
            {isSubmitting ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
      </div>
    </main>
  );
}
