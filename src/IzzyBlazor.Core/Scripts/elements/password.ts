/**
 * A password TextField's helps, loaded by <ib-field-control> on pages that have one (not an element):
 * - "Caps Lock is on." under the field while it is, read from each key in the field and gone when
 *   the field is left. The status is there from the start, so filling it is announced.
 * - With Requirements, each listed rule ticks as the password is typed: data-met for CSS, and "met"
 *   or "not yet" in its text for screen readers. When the last one is met, "All requirements met."
 *   is announced once; nothing is announced per keystroke. The rules are the server's, which checks
 *   them again when the form is posted.
 */
export function setupPassword(control: HTMLElement, input: HTMLInputElement, signal: AbortSignal): void {
  const field = control.closest(".ib-field");
  const caps = field?.querySelector<HTMLElement>(":scope > .ib-field-capslock");
  const capsStatus = caps?.querySelector<HTMLElement>("[role=status]");
  if (caps && capsStatus) {
    const show = (on: boolean) => {
      const text = on ? (caps.dataset.ibCapslock ?? "") : "";
      if (capsStatus.textContent !== text) capsStatus.textContent = text;
    };
    const read = (event: KeyboardEvent) => {
      // Unknown on some keys and platforms: only a real answer changes it.
      if (typeof event.getModifierState === "function") show(event.getModifierState("CapsLock"));
    };
    input.addEventListener("keydown", read, { signal });
    input.addEventListener("keyup", read, { signal });
    input.addEventListener("blur", () => show(false), { signal });
  }

  const list = input.id ? document.getElementById(`${input.id}-rules`) : null;
  const status = field?.querySelector<HTMLElement>(":scope > .ib-password-status");
  if (!list) return;
  const checks: Record<string, (value: string, n: number) => boolean> = {
    length: (v, n) => v.length >= n,
    lower: (v) => /[a-z]/.test(v),
    upper: (v) => /[A-Z]/.test(v),
    digit: (v) => /[0-9]/.test(v),
    // As .NET's char.IsLetterOrDigit: letters and decimal digits.
    symbol: (v) => /[^\p{L}\p{Nd}]/u.test(v),
    unique: (v, n) => new Set(v.split("")).size >= n,
  };
  const items = [...list.querySelectorAll<HTMLElement>(":scope > li[data-rule]")];
  // The state's text is this script's: without it, nothing would say "met" truthfully.
  const states = items.map((item) => {
    let state = item.querySelector<HTMLElement>(":scope > .ib-rule-state");
    if (!state) {
      state = document.createElement("span");
      state.className = "ib-rule-state ib-visually-hidden";
      item.append(state);
    }
    return state;
  });
  let allMet: boolean | null = null;
  const update = () => {
    let met = 0;
    items.forEach((item, i) => {
      const ok = checks[item.dataset.rule ?? ""]?.(input.value, Number(item.dataset.n ?? 0)) ?? false;
      if (ok) met++;
      item.toggleAttribute("data-met", ok);
      const text = `, ${ok ? list.dataset.ibMet : list.dataset.ibUnmet}`;
      if (states[i].textContent !== text) states[i].textContent = text;
    });
    const all = input.value.length > 0 && met === items.length;
    // Announced as it becomes true while typing, not for what the page came with.
    if (status && allMet !== null && all !== allMet) status.textContent = all ? (list.dataset.ibAll ?? "") : "";
    allMet = all;
  };
  input.addEventListener("input", update, { signal });
  input.form?.addEventListener("reset", () => setTimeout(update), { signal });
  update();
}
