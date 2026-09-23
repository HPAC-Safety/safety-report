import { useEffect } from "react"

import { carriesFiles } from "./AttachmentField"

/**
 * While the form is mounted, a file dropped anywhere but an attachment drop
 * zone is ignored. Left alone, the browser would open the file in place of the
 * form and the reporter would lose the page they were on. A drop zone handles
 * its own drop and calls preventDefault first, so this only catches the rest.
 */
export function useStrayFileDropGuard() {
	useEffect(() => {
		function refuse(event: DragEvent) {
			if (event.defaultPrevented || !carriesFiles(event)) return
			event.preventDefault()
			if (event.dataTransfer) event.dataTransfer.dropEffect = "none"
		}
		window.addEventListener("dragover", refuse)
		window.addEventListener("drop", refuse)
		return () => {
			window.removeEventListener("dragover", refuse)
			window.removeEventListener("drop", refuse)
		}
	}, [])
}
