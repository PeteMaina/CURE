import { UserManager, WebStorageStateStore } from "oidc-client-ts";

const authority = process.env.CURE_OIDC_AUTHORITY;
const clientId = process.env.CURE_OIDC_CLIENT_ID;
const scope = process.env.CURE_OIDC_SCOPE;

export const isOidcConfigured = Boolean(authority && clientId);

export const oidcManager = isOidcConfigured
  ? new UserManager({
      authority,
      client_id: clientId,
      redirect_uri: `${window.location.origin}/auth/callback`,
      post_logout_redirect_uri: `${window.location.origin}/login`,
      response_type: "code",
      scope,
      loadUserInfo: false,
      monitorSession: false,
      userStore: new WebStorageStateStore({ store: window.sessionStorage }),
    })
  : null;

export async function beginOidcLogin() {
  if (!oidcManager) {
    throw new Error("Production sign-in is not configured for this deployment.");
  }
  await oidcManager.signinRedirect();
}

export async function completeOidcLogin() {
  if (!oidcManager) {
    throw new Error("Production sign-in is not configured for this deployment.");
  }
  await oidcManager.signinRedirectCallback();
}

export async function getAccessToken() {
  if (oidcManager) {
    const user = await oidcManager.getUser();
    if (user && !user.expired) return user.access_token;
    return null;
  }
  return sessionStorage.getItem("cure.accessToken");
}
