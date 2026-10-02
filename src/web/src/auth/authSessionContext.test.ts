import { describe, expect, it } from "vitest"
import { AuthContext } from "./authSessionContext"

describe("authSessionContext", () => {
	it("exports a context", () => {
		expect(AuthContext).toBeDefined()
	})
})
