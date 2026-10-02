/**
 * Where to go after signing in: the page that sent the member here, such as a
 * report they wanted to comment on, but only a path on this site. Anything
 * else — another origin, a protocol-relative URL — goes home.
 */
export function returnTarget(requested: string | null): string {
	return requested && requested.startsWith("/") && !requested.startsWith("//") && !requested.startsWith("/\\") ? requested : "/"
}
