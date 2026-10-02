// The little bit of the game that has to be JavaScript: the save slot and turning a cover into a PNG.
window.nemesis = {
  load(key) {
    try { return localStorage.getItem(key); } catch { return null; }
  },
  save(key, value) {
    try { localStorage.setItem(key, value); } catch { /* private mode: the game still runs, it just won't remember */ }
  },

  fonts: null,
  async fontCss() {
    if (this.fonts) return this.fonts;
    const toData = async (url) => {
      const buf = await (await fetch(url)).arrayBuffer();
      let bin = '';
      const bytes = new Uint8Array(buf);
      for (let i = 0; i < bytes.length; i += 0x8000) bin += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
      return 'data:font/woff2;base64,' + btoa(bin);
    };
    try {
      this.fonts = `@font-face{font-family:Bangers;src:url(${await toData('fonts/bangers-regular.woff2')}) format('woff2');}`;
    } catch { this.fonts = ''; }
    return this.fonts;
  },

  // An SVG drawn in the page has its patterns and fonts outside it; copy them in, then rasterise.
  async downloadCover(id, filename) {
    const svg = document.getElementById(id);
    if (!svg) return;
    const clone = svg.cloneNode(true);
    clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
    clone.setAttribute('width', '800');
    clone.setAttribute('height', '1200');
    const defs = document.getElementById('ink-defs');
    if (defs) clone.insertBefore(defs.cloneNode(true), clone.firstChild);
    const style = document.createElementNS('http://www.w3.org/2000/svg', 'style');
    style.textContent = await this.fontCss();
    clone.insertBefore(style, clone.firstChild);
    const url = URL.createObjectURL(new Blob([new XMLSerializer().serializeToString(clone)], { type: 'image/svg+xml' }));
    try {
      const img = new Image();
      await new Promise((ok, fail) => { img.onload = ok; img.onerror = fail; img.src = url; });
      const canvas = document.createElement('canvas');
      canvas.width = 800; canvas.height = 1200;
      canvas.getContext('2d').drawImage(img, 0, 0, 800, 1200);
      const blob = await new Promise((ok) => canvas.toBlob(ok, 'image/png'));
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      a.remove();
      setTimeout(() => URL.revokeObjectURL(a.href), 2000);
    } finally {
      URL.revokeObjectURL(url);
    }
  },
};
