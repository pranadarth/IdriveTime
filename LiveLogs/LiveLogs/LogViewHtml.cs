namespace LiveLogs
{
    internal static class LogViewHtml
    {
        // Note: kept free of double quotes so it can live in a verbatim string.
        public const string Page = @"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>
:root { --bg:#ffffff; --fg:#1e1e1e; --gutter:#9a9a9a; --sel:#add6ff; --hit:#ffe58a; --cur:#ff9800; --scroll:#c8c8c8; --track:#f0f0f0; --hover:rgba(0,0,0,0.04); }
body.dark { --bg:#1e1e1e; --fg:#d4d4d4; --gutter:#6e6e6e; --sel:#264f78; --hit:#6b5a1e; --cur:#d17a00; --scroll:#505050; --track:#2d2d2d; --hover:rgba(255,255,255,0.04); }
html, body { margin:0; padding:0; height:100%; background:var(--bg); color:var(--fg); }
body { font-family:Consolas,'Cascadia Mono',monospace; font-size:13px; line-height:18px; overflow:hidden; }
#log { position:absolute; inset:0; overflow:auto; padding:4px 0; }
.chunk { content-visibility:auto; contain-intrinsic-size:auto 3600px; }
.l { white-space:pre-wrap; word-break:break-all; padding:0 10px 0 64px; position:relative; min-height:18px; }
.l:hover { background:var(--hover); }
.l::before { content:attr(data-n); position:absolute; left:0; width:54px; text-align:right; color:var(--gutter); user-select:none; }
#partial { opacity:0.85; }
::selection { background:var(--sel); }
::highlight(hit) { background-color:var(--hit); }
::highlight(cur) { background-color:var(--cur); color:#000; }
#jump { position:fixed; right:22px; bottom:16px; display:none; cursor:pointer; border:none; border-radius:16px; padding:6px 12px;
        background:var(--cur); color:#000; font:12px 'Segoe UI',sans-serif; box-shadow:0 2px 6px rgba(0,0,0,.3); }
::-webkit-scrollbar { width:10px; height:10px; }
::-webkit-scrollbar-track { background:var(--track); }
::-webkit-scrollbar-thumb { background:var(--scroll); border-radius:5px; }
</style>
</head>
<body>
<div id='log'><div id='partial' class='l'></div></div>
<button id='jump' title='Scroll to end and follow new lines'>&#8595; Follow</button>
<script>
(function () {
  var CHUNK = 200;            // lines per chunk element
  var MAX_LINES = 500000;     // older lines get dropped beyond this
  var MAX_HIGHLIGHTS = 20000;

  var log = document.getElementById('log');
  var partialEl = document.getElementById('partial');
  var jump = document.getElementById('jump');

  var chunks = [];   // { el, lines:[string], first:int(absolute line no.) }
  var total = 0;     // absolute count of lines ever appended (for numbering)
  var base = 0;      // absolute number of first retained line
  var follow = true;

  var query = '', matches = [], cur = -1;
  var hasHL = typeof Highlight !== 'undefined' && CSS.highlights;

  function atBottom() { return log.scrollHeight - log.scrollTop - log.clientHeight < 30; }
  log.addEventListener('scroll', function () {
    follow = atBottom();
    jump.style.display = follow ? 'none' : 'block';
  });
  jump.addEventListener('click', function () { follow = true; log.scrollTop = log.scrollHeight; jump.style.display = 'none'; });

  function newChunk() {
    var el = document.createElement('div');
    el.className = 'chunk';
    log.insertBefore(el, partialEl);
    var c = { el: el, lines: [], first: total };
    chunks.push(c);
    return c;
  }

  function append(lines) {
    if (!lines.length) return;
    var c = chunks.length ? chunks[chunks.length - 1] : null;
    var frag = null, fragChunk = null;
    for (var i = 0; i < lines.length; i++) {
      if (!c || c.lines.length >= CHUNK) {
        if (frag) fragChunk.el.appendChild(frag);
        c = newChunk(); frag = null;
      }
      if (!frag) { frag = document.createDocumentFragment(); fragChunk = c; }
      var d = document.createElement('div');
      d.className = 'l';
      total++;
      d.setAttribute('data-n', total);
      d.textContent = lines[i];
      frag.appendChild(d);
      c.lines.push(lines[i]);
    }
    if (frag) fragChunk.el.appendChild(frag);

    var dropped = false;
    while (total - base > MAX_LINES && chunks.length > 1) {
      var old = chunks.shift();
      base += old.lines.length;
      old.el.remove();
      dropped = true;
    }
    if (query) {
      if (dropped) research(false); else searchFrom(lines.length);
    }
  }

  function clearAll() {
    for (var i = 0; i < chunks.length; i++) chunks[i].el.remove();
    chunks = []; total = 0; base = 0;
    partialEl.textContent = '';
    matches = []; cur = -1;
    if (hasHL) { CSS.highlights.delete('hit'); CSS.highlights.delete('cur'); }
  }

  // ---------- search ----------
  function lineEl(abs) {
    var rel = abs - base;
    var c = chunks[Math.floor(rel / CHUNK)];
    // chunk boundaries stay aligned to CHUNK since we only ever drop whole chunks from the front
    return c ? c.el.children[abs - c.first] : null;
  }
  function makeRange(m) {
    var el = lineEl(m.line);
    if (!el || !el.firstChild) return null;
    var r = new Range();
    r.setStart(el.firstChild, m.at);
    r.setEnd(el.firstChild, m.at + query.length);
    return r;
  }
  function scanChunk(c, startIdx, q, out) {
    for (var i = startIdx; i < c.lines.length; i++) {
      var s = c.lines[i].toLowerCase(), p = s.indexOf(q);
      while (p >= 0) { out.push({ line: c.first + i, at: p }); p = s.indexOf(q, p + q.length); }
    }
  }
  function paintAll() {
    if (!hasHL) return;
    var hl = new Highlight();
    var n = Math.min(matches.length, MAX_HIGHLIGHTS);
    for (var i = 0; i < n; i++) { var r = makeRange(matches[i]); if (r) hl.add(r); }
    CSS.highlights.set('hit', hl);
  }
  function paintCurrent(scroll) {
    if (cur < 0 || cur >= matches.length) { if (hasHL) CSS.highlights.delete('cur'); return; }
    var m = matches[cur];
    var el = lineEl(m.line);
    if (scroll && el) {
      follow = false;
      el.scrollIntoView({ block: 'center' });
      jump.style.display = atBottom() ? 'none' : 'block';
    }
    if (hasHL) { var r = makeRange(m); if (r) CSS.highlights.set('cur', new Highlight(r)); }
  }
  function status() { return matches.length ? (cur + 1) + '/' + matches.length : (query ? '0/0' : ''); }

  function research(jumpToFirst) {
    matches = [];
    var q = query.toLowerCase();
    if (q) for (var i = 0; i < chunks.length; i++) scanChunk(chunks[i], 0, q, matches);
    if (jumpToFirst) cur = matches.length ? 0 : -1;
    else if (cur >= matches.length) cur = matches.length - 1;
    paintAll();
    paintCurrent(jumpToFirst);
  }
  function searchFrom(newCount) {
    // incremental: scan only the newly appended lines
    var q = query.toLowerCase(), startAbs = total - newCount, found = [];
    for (var i = 0; i < chunks.length; i++) {
      var c = chunks[i];
      if (c.first + c.lines.length <= startAbs) continue;
      scanChunk(c, Math.max(0, startAbs - c.first), q, found);
    }
    if (!found.length) return;
    matches = matches.concat(found);
    if (cur < 0) cur = 0;
    paintAll();
    paintCurrent(false);
  }

  window.api = {
    search: function (q) { query = q || ''; research(true); return status(); },
    next: function () { if (matches.length) { cur = (cur + 1) % matches.length; paintCurrent(true); } return status(); },
    prev: function () { if (matches.length) { cur = (cur - 1 + matches.length) % matches.length; paintCurrent(true); } return status(); },
    theme: function (dark) { document.body.classList.toggle('dark', !!dark); }
  };

  window.chrome.webview.addEventListener('message', function (e) {
    var msg = e.data;
    var stick = follow;
    if (msg.c) clearAll();
    append(msg.l);
    partialEl.textContent = msg.p || '';
    if (stick) log.scrollTop = log.scrollHeight;
  });

  document.addEventListener('keydown', function (e) {
    if (e.ctrlKey && (e.key === 'f' || e.key === 'F')) { e.preventDefault(); chrome.webview.postMessage('find'); }
    else if (e.key === 'F3' || e.key === 'Enter') { e.preventDefault(); chrome.webview.postMessage(e.shiftKey ? 'prev' : 'next'); }
    else if (e.key === 'End' && e.ctrlKey) { follow = true; }
  });
})();
</script>
</body>
</html>";
    }
}
