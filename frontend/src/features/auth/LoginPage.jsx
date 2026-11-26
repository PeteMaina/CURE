import { useState } from "react";
import { LockOutlined, LoginOutlined } from "@mui/icons-material";
import { beginOidcLogin, isOidcConfigured } from "../../services/oidc";
import { authApi } from "../../services/api";

const isDevelopment = process.env.NODE_ENV === "development";

export default function LoginPage() {
  const [password, setPassword] = useState("");
  const [state, setState] = useState({ status: "idle", message: "" });

  async function submitDevelopmentLogin(event) {
    event.preventDefault();
    setState({ status: "submitting", message: "" });
    try {
      const result = await authApi.developmentToken(password);
      sessionStorage.setItem("cure.accessToken", result.accessToken);
      window.location.assign("/");
    } catch (error) {
      setState({ status: "error", message: error.message });
    }
  }

  async function submitOidcLogin() {
    setState({ status: "submitting", message: "" });
    try {
      await beginOidcLogin();
    } catch (error) {
      setState({ status: "error", message: error.message });
    }
  }

  return (
    <div className="login-page">
      <div className="login-panel">
        <div className="login-brand">
          <span className="login-mark">C</span>
          <strong>CURE</strong>
          <small>The World Class CRM</small>
        </div>
        <p className="eyebrow">Secure access</p>
        <h1>Sign in</h1>
        {isOidcConfigured && (
          <button
            className="button button-primary"
            type="button"
            onClick={submitOidcLogin}
            disabled={state.status === "submitting"}
          >
            <LoginOutlined />
            {state.status === "submitting" ? "Connecting..." : "Continue securely"}
          </button>
        )}
        {isDevelopment && (
          <>
            <p className="page-subtitle">
              Local development sign-in. This credential is available only in
              the Development environment.
            </p>
            <form
              onSubmit={submitDevelopmentLogin}
              className="login-form"
              aria-label="Development sign-in"
            >
              <label>
                Development password
                <input
                  type="password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  autoComplete="current-password"
                  autoFocus={!isOidcConfigured}
                />
              </label>
              <button
                className="button button-primary"
                type="submit"
                disabled={state.status === "submitting"}
              >
                <LoginOutlined />
                {state.status === "submitting" ? "Signing in..." : "Sign in"}
              </button>
            </form>
          </>
        )}
        {!isDevelopment && !isOidcConfigured && (
          <div className="inline-error" role="alert">
            Sign-in is not configured for this deployment. Contact your CURE
            administrator.
          </div>
        )}
        {state.message && (
          <p className={`login-message ${state.status}`} role="alert">
            {state.message}
          </p>
        )}
        <small className="login-note">
          <LockOutlined />
          {isDevelopment
            ? "Development authentication is isolated from production identity."
            : "Authentication is handled by your configured identity provider."}
        </small>
      </div>
    </div>
  );
}
