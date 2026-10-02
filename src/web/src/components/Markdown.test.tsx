import { render, cleanup } from "@testing-library/react"
import { afterEach, describe, expect, it } from "vitest"
import { Markdown } from "./Markdown"

afterEach(cleanup)

describe("Markdown", () => {
	it("renders the safe subset and carries lang, class and data attributes on the wrapper", () => {
		const { container } = render(
			<Markdown lang="fr-CA" className="x" data-summary="yes">
				{"## Title\n\nSome **bold** and *italic*\nsecond line\n\n- one\n- two\n\n1. first"}
			</Markdown>,
		)
		const wrapper = container.firstElementChild as HTMLElement
		expect(wrapper.getAttribute("lang")).toBe("fr-CA")
		expect(wrapper.className).toBe("x")
		expect(wrapper.getAttribute("data-summary")).toBe("yes")
		expect(wrapper.querySelector("h2")?.textContent).toBe("Title")
		expect(wrapper.querySelector("strong")?.textContent).toBe("bold")
		expect(wrapper.querySelector("em")?.textContent).toBe("italic")
		expect(wrapper.querySelector("br")).not.toBeNull()
		expect(wrapper.querySelectorAll("ul li")).toHaveLength(2)
		expect(wrapper.querySelectorAll("ol li")).toHaveLength(1)
	})

	it("shifts headings deeper by the offset and stops at h6", () => {
		const { container } = render(<Markdown headingOffset={3}>{"# a\n\n## b\n\n### c\n\n#### d\n\n##### e\n\n###### f"}</Markdown>)
		expect([...container.querySelectorAll("h1,h2,h3,h4,h5,h6")].map((heading) => heading.tagName)).toEqual(["H4", "H5", "H6", "H6", "H6", "H6"])
	})

	it("shows raw HTML, links and images as plain text or nothing", () => {
		const { container } = render(<Markdown>{"<script>x</script> [link](http://a.b) ![img](http://a.b/i.png)"}</Markdown>)
		expect(container.querySelector("script, a, img")).toBeNull()
		expect(container.textContent).toContain("link")
	})

	it("reuses one set of renderers per offset across renders", () => {
		const first = render(<Markdown headingOffset={1}>{"# a"}</Markdown>)
		const second = render(<Markdown headingOffset={1}>{"# a"}</Markdown>)
		expect(second.container.innerHTML).toBe(first.container.innerHTML)
	})
})
