(() => {
  'use strict';
  const root = document.querySelector('[data-feedback]');
  if (!root) return;
  const api = 'https://api.github.com/repos/harryoke/hazz-karaoke-hoster/issues/1';
  const thread = 'https://github.com/harryoke/hazz-karaoke-hoster/issues/1';
  const status = root.querySelector('[data-feedback-status]');
  const list = root.querySelector('[data-feedback-list]');
  const refresh = root.querySelector('[data-feedback-refresh]');
  const earlier = root.querySelector('[data-feedback-earlier]');
  const later = root.querySelector('[data-feedback-later]');
  let page = 1, lastPage = 1, busy = false;

  function render(comments) {
    const fragment = document.createDocumentFragment();
    for (const comment of [...comments].reverse()) {
      if (!Number.isSafeInteger(comment.id)) continue;
      const article = document.createElement('article');
      article.className = 'feedback-comment';
      const header = document.createElement('p');
      header.className = 'feedback-byline';
      const author = document.createElement('strong');
      author.textContent = comment.user?.login || 'GitHub user';
      header.append(author);
      const date = new Date(comment.created_at);
      if (!Number.isNaN(date.getTime())) {
        const time = document.createElement('time');
        time.dateTime = date.toISOString();
        time.textContent = date.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
        header.append(time);
      }
      const body = document.createElement('p');
      body.className = 'feedback-body';
      // Render user comments as text, never HTML or executable Markdown.
      const content = typeof comment.body === 'string' ? comment.body : '';
      body.textContent = content.length > 6000 ? content.slice(0, 6000) + '…' : content;
      const link = document.createElement('a');
      link.href = thread + '#issuecomment-' + comment.id;
      link.textContent = 'View or reply on GitHub';
      article.append(header, body, link);
      fragment.append(article);
    }
    list.replaceChildren(fragment);
  }

  async function load(latest = false) {
    if (busy) return;
    busy = true;
    root.setAttribute('aria-busy', 'true');
    refresh.disabled = earlier.disabled = later.disabled = true;
    status.textContent = 'Loading feedback…';
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 12000);
    async function get(url) {
      const response = await fetch(url, { signal: controller.signal, credentials: 'omit', headers: { Accept: 'application/vnd.github+json' } });
      if (!response.ok) throw new Error('Feedback unavailable');
      return response.json();
    }
    try {
      if (latest) {
        const issue = await get(api);
        if (!Number.isSafeInteger(issue.comments) || issue.comments < 0) throw new Error('Invalid feedback count');
        lastPage = Math.max(1, Math.ceil(issue.comments / 20));
        page = lastPage;
      }
      const comments = await get(api + '/comments?per_page=20&page=' + page);
      if (!Array.isArray(comments)) throw new Error('Invalid feedback response');
      render(comments);
      status.textContent = comments.length === 0 ? 'No comments yet — be the first to leave feedback.' :
        'Newest comments first · Page ' + page + ' of ' + lastPage + '. Comments are public.';
    } catch {
      status.textContent = 'Comments could not be refreshed. You can still read and post feedback using the GitHub link below.';
    } finally {
      clearTimeout(timeout);
      busy = false;
      root.setAttribute('aria-busy', 'false');
      refresh.disabled = false;
      earlier.disabled = page <= 1;
      later.disabled = page >= lastPage;
    }
  }
  refresh.addEventListener('click', () => load(true));
  earlier.addEventListener('click', () => { if (!busy && page > 1) { page--; load(); } });
  later.addEventListener('click', () => { if (!busy && page < lastPage) { page++; load(); } });
  load(true);
})();
