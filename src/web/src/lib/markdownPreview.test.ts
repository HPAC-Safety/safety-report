import { describe, expect, it } from "vitest"

import { firstSectionPreview } from "./markdownPreview"

describe("firstSectionPreview", () => {
	it("previews a summary with no heading whole", () => {
		expect(firstSectionPreview("Just a plain summary.\nSecond line.")).toBe("Just a plain summary.\nSecond line.")
	})

	it("returns the body of the first section without its heading", () => {
		expect(firstSectionPreview("## What happened\nThe aircraft landed.\n\n## Why\nWind.")).toBe("The aircraft landed.")
	})

	it("reads to the end when there is only one heading", () => {
		expect(firstSectionPreview("# Title\nBody one.\nBody two.")).toBe("Body one.\nBody two.")
	})

	it("skips text before the first heading", () => {
		expect(firstSectionPreview("Intro\n### Section\nBody")).toBe("Body")
	})

	it("treats a bare hash line and an indented heading as headings", () => {
		expect(firstSectionPreview("   ##\nBody\n#\nOther")).toBe("Body")
	})

	it("does not treat seven hashes or a hash without a space as a heading", () => {
		expect(firstSectionPreview("#tag text\n####### seven")).toBe("#tag text\n####### seven")
	})

	it("normalizes carriage returns", () => {
		expect(firstSectionPreview("## H\r\nLine one\rLine two")).toBe("Line one\nLine two")
	})

	it("strips Markdown characters", () => {
		const md = "## H\n![alt](img.png)text [link](http://x.y) **bold** __bold2__ *em* _em2_ `code`\n- item\n1. one\n2) two"
		expect(firstSectionPreview(md)).toBe("text link bold bold2 em em2 code\nitem\none\ntwo")
	})

	it("trims trailing spaces and collapses blank lines", () => {
		expect(firstSectionPreview("## H\nA  \n\n\n\nB")).toBe("A\n\nB")
	})

	it("returns an empty string for an empty section or input", () => {
		expect(firstSectionPreview("")).toBe("")
		expect(firstSectionPreview("## H\n## Next\nBody")).toBe("")
	})
})
