// ChatGPT のページで javascript_tool に流す。送った文言の一部（marker）より後ろに出た生成画像を、
// 生成が終わってから（停止ボタンと「プレビュー」が消えてから）c3td_{id}.png としてダウンロードする。
// javascript_tool は 45 秒で切れるので、timeoutMs は 30000 くらいにして、出ていなければもう一度呼ぶ。
// 例：await window.c3tdGrab2('tsuuchi', 'スマホのメッセージ通知', 30000)
window.c3tdBusy = function () {
  const stop = Array.from(document.querySelectorAll('button')).some(b => (b.getAttribute('aria-label') || '') === '停止' || /stop/i.test(b.dataset.testid || ''));
  const preview = Array.from(document.querySelectorAll('div,span')).some(e => e.childElementCount === 0 && e.textContent.trim() === 'プレビュー');
  return stop || preview;
};
window.c3tdFind = function (marker) {
  if (window.c3tdBusy()) return null;
  const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
  let markNode = null, n;
  while ((n = walker.nextNode())) if (n.nodeValue.includes(marker)) markNode = n;
  if (!markNode) return null;
  const imgs = Array.from(document.querySelectorAll('img[alt^="生成画像"]')).filter(i => i.complete && i.naturalWidth >= 1000 && (markNode.compareDocumentPosition(i) & Node.DOCUMENT_POSITION_FOLLOWING));
  return imgs.length ? imgs[0] : null;
};
window.c3tdGrab2 = async function (id, marker, timeoutMs) {
  const t0 = Date.now();
  let img = null;
  while (Date.now() - t0 < timeoutMs) {
    img = window.c3tdFind(marker);
    if (img) break;
    await new Promise(r => setTimeout(r, 2000));
  }
  if (!img) return { id, ready: false, waited: Math.round((Date.now() - t0) / 1000) };
  await new Promise(r => setTimeout(r, 2500));
  img = window.c3tdFind(marker);
  const blob = await fetch(img.src).then(r => r.blob());
  const bmp = await createImageBitmap(blob);
  const c = document.createElement('canvas'); c.width = bmp.width; c.height = bmp.height;
  const g = c.getContext('2d'); g.drawImage(bmp, 0, 0);
  const corner = Array.from(g.getImageData(5, 5, 1, 1).data);
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob); a.download = 'c3td_' + id + '.png';
  document.body.appendChild(a); a.click(); a.remove();
  return { id, ready: true, size: blob.size, w: bmp.width, h: bmp.height, cornerAlpha: corner[3], waited: Math.round((Date.now() - t0) / 1000) };
};
