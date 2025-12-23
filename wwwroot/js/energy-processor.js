class EnergyProcessor extends AudioWorkletProcessor {
  static get parameterDescriptors() {
    return [];
  }

  constructor() {
    super();
    this.buffer = [];
    this.recording = false;
    this.activeFrames = 0;
    this.ENERGY_THRESHOLD = 0.02;
    this.MIN_FRAMES = 5;
    this.MAX_FRAMES = 60;
  }

  process(inputs, outputs, parameters) {
    const input = inputs[0];
    if (input.length === 0) return true;

    const channelData = input[0];
    let rms = 0;
    for (let i = 0; i < channelData.length; i++) {
      rms += channelData[i] * channelData[i];
    }
    rms = Math.sqrt(rms / channelData.length);

    if (rms > this.ENERGY_THRESHOLD) {
      this.activeFrames++;
      this.recording = true;
    }

    if (this.recording) {
      this.buffer.push(new Float32Array(channelData));

      if (this.activeFrames >= this.MIN_FRAMES && this.buffer.length >= this.MAX_FRAMES) {
        this.port.postMessage({ samples: this.concatBuffer() });
        this.reset();
      }
    }

    if (this.recording && rms < this.ENERGY_THRESHOLD && this.activeFrames >= this.MIN_FRAMES) {
      this.port.postMessage({ samples: this.concatBuffer() });
      this.reset();
    }

    return true;
  }

  concatBuffer() {
    const length = this.buffer.reduce((a, b) => a + b.length, 0);
    const samples = new Float32Array(length);
    let offset = 0;
    for (const chunk of this.buffer) {
      samples.set(chunk, offset);
      offset += chunk.length;
    }
    return Array.from(samples); // Für Blazor einfacher
  }

  reset() {
    this.buffer = [];
    this.recording = false;
    this.activeFrames = 0;
  }
}

registerProcessor('energy-processor', EnergyProcessor);
