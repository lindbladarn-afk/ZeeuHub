(function () {
  if (window.__hubNotificationsInitialized) {
    return;
  }

  window.__hubNotificationsInitialized = true;

  const copyText = async (text) => {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text);
      return true;
    }

    const helper = document.createElement('textarea');
    helper.value = text;
    helper.setAttribute('readonly', 'readonly');
    helper.style.position = 'absolute';
    helper.style.left = '-9999px';
    document.body.appendChild(helper);
    helper.select();
    const succeeded = document.execCommand('copy');
    document.body.removeChild(helper);
    return succeeded;
  };

  const setCopiedState = (toast, copied) => {
    const button = toast.querySelector('[data-hub-password-copy]');

    if (button) {
      button.classList.toggle('is-copied', copied);
      button.innerHTML = copied
        ? '<i class="fas fa-check" aria-hidden="true"></i>'
        : '<i class="fas fa-copy" aria-hidden="true"></i>';
      button.setAttribute('aria-label', copied ? 'Lösenord kopierat' : 'Kopiera lösenord');
      button.title = copied ? 'Lösenord kopierat' : 'Kopiera lösenord';
    }
  };

  document.addEventListener('click', async (event) => {
    const button = event.target instanceof Element
      ? event.target.closest('[data-hub-password-copy]')
      : null;

    if (!button) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();

    const toast = button.closest('[data-hub-password-toast]');
    const valueNode = toast?.querySelector('[data-hub-password-value]');
    const value = valueNode?.textContent?.trim() || toast?.getAttribute('data-hub-password-value') || '';

    if (!value) {
      return;
    }

    try {
      await copyText(value);
      setCopiedState(toast, true);
      window.setTimeout(() => {
        if (toast?.isConnected) {
          setCopiedState(toast, false);
        }
      }, 1500);
    } catch {
      setCopiedState(toast, false);
    }
  }, true);
})();
