// One random id per browser tab/session - sent as the X-ErrorLog-Client-Id header on every
// resolve/comment mutation, and echoed back in the matching SignalR broadcast so this tab can
// tell "an update I already applied optimistically" apart from "a change made elsewhere".
export const ERROR_LOG_CLIENT_ID = crypto.randomUUID();
