// The Chat docs' dictation example: the browser's speech recognition, reporting what it hears to
// the component. Chrome, Edge and Safari have it (Chrome and Edge send the audio to their speech
// services); Firefox doesn't.
let recognition = null;

export function supported() {
  return "SpeechRecognition" in window || "webkitSpeechRecognition" in window;
}

export function start(component) {
  const Recognition = window.SpeechRecognition ?? window.webkitSpeechRecognition;
  recognition = new Recognition();
  recognition.interimResults = true;
  recognition.onresult = (event) => {
    const text = [...event.results].map((result) => result[0].transcript).join("");
    component.invokeMethodAsync("Heard", text);
  };
  recognition.onend = () => component.invokeMethodAsync("Ended");
  recognition.start();
}

export function stop() {
  recognition?.stop();
}
