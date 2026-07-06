// The backend returns RFC7807 ProblemDetails on failure - FluentValidation errors land in an
// "errors" extension (e.g. { Comment: ["A comment is required..."] }), everything else at least
// has a "title". Falling back through both keeps a real message on screen instead of a bare
// "Request failed: 400" whenever something is actually wrong.
export async function throwIfNotOk(response: Response): Promise<Response> {
  if (response.ok) {
    return response;
  }

  let message = `Request failed: ${response.status} ${response.statusText}`;
  try {
    const body = await response.clone().json();
    const firstFieldError = body?.errors && Object.values(body.errors).flat()[0];
    message = (firstFieldError as string | undefined) ?? body?.title ?? message;
  } catch {
    // Body wasn't JSON (or was empty) - keep the status-based message above.
  }

  throw new Error(message);
}
