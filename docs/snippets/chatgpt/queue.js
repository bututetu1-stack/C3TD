// ChatGPT のページで流す：文言を順に送り、生成が終わるたびに c3td_{id}.png としてダウンロードする（c3tdGrab2 が必要）
window.c3tdSend = async function (text) {
  const box = document.querySelector('#prompt-textarea') || document.querySelector('[contenteditable="true"]');
  if (!box) return 'no box';
  box.focus();
  document.execCommand('selectAll', false, null);
  document.execCommand('insertText', false, text);
  await new Promise(r => setTimeout(r, 600));
  const send = document.querySelector('button[data-testid="send-button"]') || document.querySelector('#composer-submit-button') || Array.from(document.querySelectorAll('button')).find(b => b.getAttribute('aria-label') === '送信');
  if (!send) return 'no send';
  send.click();
  return 'sent';
};
window.c3tdQueue = async function (items) {
  window.__queue = window.__queue || [];
  for (const it of items) {
    // 前の生成が終わるまで待つ
    for (let i = 0; i < 200 && window.c3tdBusy(); i++) await new Promise(r => setTimeout(r, 2000));
    await new Promise(r => setTimeout(r, 1500));
    const s = await window.c3tdSend(it.prompt);
    window.__queue.push({ id: it.id, step: s, at: new Date().toISOString() });
    if (s !== 'sent') continue;
    await new Promise(r => setTimeout(r, 4000));
    const g = await window.c3tdGrab2(it.id, it.marker, 300000);
    window.__queue.push(g);
  }
  window.__queue.push({ done: true });
};
