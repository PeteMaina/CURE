import { useEffect, useRef, useState } from "react";
import { completeOidcLogin } from "../../services/oidc";

export default function OidcCallbackPage() {
  const [error, setError] = useState("");
  const callbackStarted = useRef(false);

  useEffect(() => {
    if (callbackStarted.current) return;
    callbackStarted.current = true;
    let active = true;
    completeOidcLogin()
      .then(() => {
        window.location.replace("/");
      })
      .catch((callbackError) => {
        if (active) setError(callbackError.message);
      });

    return () => {
      active = false;
    };
  }, []);

  return (
    <div className="login-page">
      <div className="login-panel" aria-live="polite">
        <p className="eyebrow">Secure access</p>
        <h1>{error ? "Sign-in could not be completed" : "Verifying sign-in"}</h1>
        {error ? (
          <>
            <p className="login-message error" role="alert">
              {error}
            </p>
            <a className="button button-secondary" href="/login">
              Return to sign in
            </a>
          </>
        ) : (
          <p className="page-subtitle">
            Your identity provider is completing the secure sign-in.
          </p>
        )}
      </div>
    </div>
  );
}
