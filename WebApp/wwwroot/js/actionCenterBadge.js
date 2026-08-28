(() => {
  const link = () => document.getElementById('actionCenterBadgeLink');
  const badge = () => document.getElementById('actionCenterBadge');
  const countEl = () => document.getElementById('actionCenterBadgeCount');

  const applyUi = ({ count, hasHighPriority, hasNew }) => {
    const lnk = link();
    const b = badge();
    const cnt = countEl();
    if (!lnk || !b || !cnt) return;

    cnt.textContent = String(count ?? 0);
    if ((count ?? 0) > 0) {
      b.classList.remove('d-none');
      b.classList.toggle('bg-danger', !!hasHighPriority);
      b.classList.toggle('bg-warning', !hasHighPriority);
      b.classList.toggle('text-dark', !hasHighPriority);
    } else {
      b.classList.add('d-none');
    }

    lnk.classList.toggle('btn-outline-danger', !!hasNew);
    lnk.classList.toggle('btn-outline-secondary', !hasNew);
  };

  const refresh = async () => {
    try {
      const res = await fetch('/ActionCenter/Summary', { method: 'GET', credentials: 'same-origin' });
      if (!res.ok) return;
      const data = await res.json();
      applyUi(data || {});
    } catch {
      // ignore
    }
  };

  const init = () => {
    refresh();
    window.setInterval(refresh, 60_000);
    window.addEventListener('focus', refresh);
  };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
