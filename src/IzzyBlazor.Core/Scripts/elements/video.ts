import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-video> plays a VideoEmbed in place. The poster is a link to the video on YouTube, which is
 * what happens with no script; with it, a plain click puts the player (youtube-nocookie.com,
 * autoplaying) in the empty frame Blazor rendered for it, and data-playing hides the poster.
 */
class VideoEmbed extends IzzyElement {
  protected setup(signal: AbortSignal): void {
    this.addEventListener(
      "click",
      (event) => {
        const poster = (event.target as Element).closest<HTMLAnchorElement>(".ib-video-poster");
        const frame = this.querySelector<HTMLElement>(".ib-video-frame");
        const src = poster?.dataset.ibVideoSrc;
        if (!poster || !frame || !src || !this.contains(poster)) return;
        // New tab, new window, download: let the link do it.
        if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        event.preventDefault();

        const player = document.createElement("iframe");
        player.src = src;
        player.title = this.dataset.title ?? "";
        player.allow = "accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share";
        player.allowFullscreen = true;
        player.referrerPolicy = "strict-origin-when-cross-origin";
        frame.replaceChildren(player);
        this.toggleAttribute("data-playing", true);
        player.focus();
      },
      { signal }
    );
  }
}

defineElement("ib-video", VideoEmbed);
