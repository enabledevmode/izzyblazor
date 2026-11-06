import { IzzyElement, defineElement } from "../izzy-element";

/**
 * <ib-color-panel> makes ColorPanel's picker, sliders and fields work, and keeps all its parts on
 * one value: a hidden field it gives the field's name, so the form posts one color (the swatch
 * radios and hex field, which post with no script, stop posting). The area is one 2D slider (Left
 * and Right saturation, Up and Down brightness), the hue and opacity ordinary sliders; a click jumps
 * there, a drag follows the pointer, Shift and Page keys move by 10. Its arithmetic and color
 * names match ColorPanelMath on the server.
 */

type Rgba = { r: number; g: number; b: number; a: number };

function parse(value: string): Rgba | null {
  let text = value.trim().replace(/^#/, "");
  if (!/^[0-9a-f]*$/i.test(text)) return null;
  if (text.length === 3) text = [...text].map((c) => c + c).join("");
  if (text.length !== 6 && text.length !== 8) return null;
  const byte = (at: number) => parseInt(text.slice(at, at + 2), 16);
  return { r: byte(0), g: byte(2), b: byte(4), a: text.length === 8 ? Math.round((byte(6) / 255) * 100) / 100 : 1 };
}

function hex(c: Rgba, alpha: boolean): string {
  const two = (n: number) => Math.round(n).toString(16).padStart(2, "0");
  return `#${two(c.r)}${two(c.g)}${two(c.b)}${alpha && c.a < 1 ? two(c.a * 255) : ""}`;
}

function toHsv(c: Rgba): [number, number, number] {
  const [r, g, b] = [c.r / 255, c.g / 255, c.b / 255];
  const max = Math.max(r, g, b);
  const delta = max - Math.min(r, g, b);
  const h = !delta ? 0 : max === r ? 60 * ((((g - b) / delta) % 6 + 6) % 6) : max === g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
  return [h, max ? delta / max : 0, max];
}

function fromHsv(h: number, s: number, v: number, a: number): Rgba {
  const f = (n: number) => {
    const k = (n + h / 60) % 6;
    return Math.round((v - v * s * Math.max(0, Math.min(k, 4 - k, 1))) * 255);
  };
  return { r: f(5), g: f(3), b: f(1), a };
}

const format = (template: string, ...values: (string | number)[]) => values.reduce<string>((t, v, i) => t.replace(`{${i}}`, String(v)), template);

let groups = 0;

class ColorPanel extends IzzyElement {
  #h = 0;
  #s = 0;
  #v = 1;
  #a = 1;
  #empty = true;

  private get names(): string[] {
    return (this.dataset.ibNames ?? "").split("|");
  }

  protected setup(signal: AbortSignal): void {
    const value = this.querySelector<HTMLInputElement>(":scope > .ib-cp-value");
    if (!value) return;
    // One field posts now: the hidden one. The swatches keep a group name of their own (so arrow
    // keys still move between them) that the server doesn't read.
    const name = this.dataset.ibName;
    if (name && !this.hasAttribute("data-ib-disabled")) {
      value.name = name;
      // A group of this panel's own: two panels outside a form would otherwise share one, and
      // choosing a swatch in one would clear the other's.
      const group = `${name}~swatch~${++groups}`;
      for (const radio of this.querySelectorAll<HTMLInputElement>(".ib-cp-swatch-input")) radio.name = group;
      this.querySelector('[data-ib-field="hex"]')?.removeAttribute("name");
    }
    this.#h = parseFloat(getComputedStyle(this).getPropertyValue("--ib-cp-hue")) || 0;
    this.take(parse(value.value), false);
    if (this.hasAttribute("data-ib-disabled")) return;

    for (const part of this.querySelectorAll<HTMLElement>(".ib-cp-area, .ib-cp-slider")) {
      part.addEventListener("pointerdown", (event) => {
        if (event.button !== 0) return;
        event.preventDefault();
        part.focus();
        part.setPointerCapture(event.pointerId);
        this.point(part, event);
      }, { signal });
      part.addEventListener("pointermove", (event) => {
        if (part.hasPointerCapture(event.pointerId)) this.point(part, event);
      }, { signal });
      part.addEventListener("pointerup", () => this.commit(), { signal });
      part.addEventListener("keydown", (event) => this.key(part, event), { signal });
    }

    this.addEventListener("change", (event) => {
      const input = event.target as HTMLInputElement;
      if (input.classList.contains("ib-cp-swatch-input")) {
        this.take(parse(input.value), true);
      } else if (input.dataset.ibField === "hex") {
        // Beside an A field, six digits keep the opacity; eight still set it.
        const color = parse(input.value);
        if (color && this.querySelector('[data-ib-field="A"]') && input.value.replace("#", "").trim().length <= 6) color.a = this.#a;
        if (color) this.take(color, true);
        else this.render();
      } else if (input.dataset.ibField) {
        const c = fromHsv(this.#h, this.#s, this.#v, this.#a);
        const n = Math.max(0, Math.min(Number(input.value) || 0, input.dataset.ibField === "A" ? 100 : 255));
        const next = { ...c, [input.dataset.ibField.toLowerCase()]: input.dataset.ibField === "A" ? n / 100 : n };
        this.take(next, true);
      }
    }, { signal });

    // The original color, chosen again.
    this.querySelector<HTMLElement>("[data-ib-original]")?.addEventListener("click", (event) => {
      this.take(parse((event.currentTarget as HTMLElement).dataset.ibOriginal!), true);
    }, { signal });

    const dropper = this.querySelector<HTMLElement>("[data-ib-eyedropper]");
    if (dropper && "EyeDropper" in window) {
      dropper.hidden = false;
      dropper.addEventListener("click", async () => {
        try {
          const Dropper = (window as unknown as { EyeDropper: new () => { open(): Promise<{ sRGBHex: string }> } }).EyeDropper;
          const { sRGBHex } = await new Dropper().open();
          this.take(parse(sRGBHex), true);
        } catch {
          // Escape, or the browser said no: nothing changes.
        }
      }, { signal });
    }
  }

  /** Takes a color (or none), keeping the hue of a gray, where it means nothing. */
  private take(color: Rgba | null, commit: boolean): void {
    this.#empty = !color;
    if (color) {
      const [h, s, v] = toHsv(color);
      if (s && v) this.#h = h;
      this.#s = s;
      this.#v = v;
      this.#a = this.hasAttribute("data-ib-alpha") ? color.a : 1;
    }
    this.render();
    this.write(commit);
  }

  /** A pointer on the area or a slider: the color at that point. */
  private point(part: HTMLElement, event: PointerEvent): void {
    const box = part.getBoundingClientRect();
    const x = Math.max(0, Math.min(1, (event.clientX - box.left) / box.width));
    const y = Math.max(0, Math.min(1, (event.clientY - box.top) / box.height));
    if (part.classList.contains("ib-cp-area")) {
      this.#s = x;
      this.#v = 1 - y;
    } else if (part.dataset.ibChannel === "hue") {
      this.#h = x * 360;
    } else {
      this.#a = Math.round(x * 100) / 100;
    }
    this.#empty = false;
    this.render();
    this.write(false);
  }

  private key(part: HTMLElement, event: KeyboardEvent): void {
    const big = event.shiftKey ? 10 : 1;
    const area = part.classList.contains("ib-cp-area");
    const hue = part.dataset.ibChannel === "hue";
    const unit = hue ? 1 / 360 : 0.01;
    let [x, y] = area ? [this.#s, this.#v] : [hue ? this.#h / 360 : this.#a, 0];
    switch (event.key) {
      case "ArrowLeft": x -= big * unit; break;
      case "ArrowRight": x += big * unit; break;
      case "ArrowUp": if (area) y += big * unit; else x += big * unit; break;
      case "ArrowDown": if (area) y -= big * unit; else x -= big * unit; break;
      case "PageUp": if (area) y += 10 * unit; else x += 10 * unit; break;
      case "PageDown": if (area) y -= 10 * unit; else x -= 10 * unit; break;
      case "Home": x = 0; break;
      case "End": x = 1; break;
      default: return;
    }
    event.preventDefault();
    const clamp = (n: number) => Math.max(0, Math.min(1, n));
    if (area) [this.#s, this.#v] = [clamp(x), clamp(y)];
    else if (hue) this.#h = clamp(x) * 360;
    else this.#a = Math.round(clamp(x) * 100) / 100;
    this.#empty = false;
    this.render();
    this.write(true);
  }

  /** The value, written to the hidden field: input as it changes, change when it's settled. */
  private write(commit: boolean): void {
    const value = this.querySelector<HTMLInputElement>(":scope > .ib-cp-value")!;
    const next = this.#empty ? "" : hex(fromHsv(this.#h, this.#s, this.#v, this.#a), this.hasAttribute("data-ib-alpha"));
    if (value.value !== next) {
      value.value = next;
      value.dispatchEvent(new Event("input", { bubbles: true }));
      if (!commit) value.dataset.ibPending = "";
    }
    if (commit) this.commit();
  }

  private commit(): void {
    const value = this.querySelector<HTMLInputElement>(":scope > .ib-cp-value")!;
    delete value.dataset.ibPending;
    value.dispatchEvent(new Event("change", { bubbles: true }));
  }

  /** Puts the color on every part: positions, values, value text, fields and the chosen swatch. */
  private render(): void {
    const names = this.names;
    const color = fromHsv(this.#h, this.#s, this.#v, this.#a);
    const opaque = hex({ ...color, a: 1 }, false);
    const pct = (n: number) => Math.round(n * 100);
    this.style.setProperty("--ib-cp-hue", String(Math.round(this.#h)));
    this.style.setProperty("--ib-cp-color", this.#empty ? "transparent" : opaque);
    this.style.setProperty("--ib-cp-alpha", String(this.#a));
    const area = this.querySelector<HTMLElement>(".ib-cp-area");
    if (area) {
      area.style.setProperty("--ib-cp-x", `${pct(this.#s)}%`);
      area.style.setProperty("--ib-cp-y", `${100 - pct(this.#v)}%`);
      area.setAttribute("aria-valuenow", String(pct(this.#s)));
      area.setAttribute("aria-valuetext", format(names[13], pct(this.#s), pct(this.#v), this.#empty ? names[15] : this.nameOf(color)));
    }
    const hue = this.querySelector<HTMLElement>('[data-ib-channel="hue"]');
    if (hue) {
      hue.style.setProperty("--ib-cp-x", `${(this.#h / 3.6).toFixed(2)}%`);
      hue.setAttribute("aria-valuenow", String(Math.round(this.#h)));
      hue.setAttribute("aria-valuetext", format(names[14], Math.round(this.#h), this.hueName(this.#h)));
    }
    const alpha = this.querySelector<HTMLElement>('[data-ib-channel="alpha"]');
    if (alpha) {
      alpha.style.setProperty("--ib-cp-x", `${pct(this.#a)}%`);
      alpha.setAttribute("aria-valuenow", String(pct(this.#a)));
      alpha.setAttribute("aria-valuetext", `${pct(this.#a)}%`);
    }
    const value = this.#empty ? "" : hex(color, this.hasAttribute("data-ib-alpha"));
    // The hex field leaves the opacity to the A field, when there is one.
    const short = this.querySelector('[data-ib-field="A"]') ? opaque : value;
    for (const field of this.querySelectorAll<HTMLInputElement>("[data-ib-field]")) {
      if (field === document.activeElement) continue;
      const key = field.dataset.ibField!;
      field.value = this.#empty ? "" : key === "hex" ? short : key === "A" ? String(pct(this.#a)) : String(color[key.toLowerCase() as "r" | "g" | "b"]);
    }
    for (const radio of this.querySelectorAll<HTMLInputElement>(".ib-cp-swatch-input")) {
      radio.checked = radio.value === value || (this.#empty && radio.value === "");
    }
  }

  /** A hue's name, with no light or dark, as ColorPanelMath.HueName. */
  private hueName(h: number): string {
    const n = this.names;
    return h < 15 || h >= 345 ? n[3] : h < 40 ? n[4] : h < 65 ? n[5] : h < 160 ? n[6] : h < 195 ? n[7] : h < 255 ? n[8] : h < 290 ? n[9] : n[10];
  }

  /** A plain name for a color, as ColorPanelMath.Name: "dark blue". */
  private nameOf(c: Rgba): string {
    const n = this.names;
    const [r, g, b] = [c.r / 255, c.g / 255, c.b / 255];
    const chroma = Math.max(r, g, b) - Math.min(r, g, b);
    // Lightness as people see it (CIE L*, 0-100), as on the server.
    const linear = (x: number) => (x <= 0.04045 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4);
    const y = 0.2126 * linear(r) + 0.7152 * linear(g) + 0.0722 * linear(b);
    const light = y > 0.008856 ? 116 * Math.cbrt(y) - 16 : 903.3 * y;
    if (light < 12) return n[0];
    if (light > 96 && chroma < 0.2) return n[1];
    const name = chroma >= 0.1 ? this.hueName(toHsv(c)[0]) : n[2];
    return light > 80 ? format(n[11], name) : light < 45 ? format(n[12], name) : name;
  }
}

defineElement("ib-color-panel", ColorPanel);
