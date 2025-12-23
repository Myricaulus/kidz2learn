let audioContext;
let workletNode;
let stream;
let source;

export async function startListening(dotNetRef) {
  if(!source || !workletNode){
    audioContext = new AudioContext();
    await audioContext.audioWorklet.addModule('js/energy-processor.js');

    stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    source = audioContext.createMediaStreamSource(stream);

    workletNode = new AudioWorkletNode(audioContext, 'EnergyProcessor');
    workletNode.port.onmessage = event => {
        dotNetRef.invokeMethodAsync("OnAudioCaptured", event.data.samples);
    };
  }

  source.connect(workletNode);
  workletNode.connect(audioContext.destination);
}

export function stopListening() {
  if (workletNode) workletNode.disconnect();
  if (stream) stream.getTracks().forEach(t => t.stop());
}

window.startListening = startListening
window.stopListening = stopListening