(() => {
  const header = document.querySelector('.site-header');
  const nav = document.querySelector('.nav');
  const toggle = document.querySelector('.nav-toggle');
  const links = document.querySelector('.nav-links');

  const closeNav = () => {
    if (!toggle || !links) return;
    toggle.setAttribute('aria-expanded', 'false');
    toggle.querySelector('.sr-only').textContent = 'Open navigation';
    links.classList.remove('is-open');
  };

  if (toggle && links) {
    toggle.addEventListener('click', () => {
      const open = toggle.getAttribute('aria-expanded') === 'true';
      toggle.setAttribute('aria-expanded', String(!open));
      toggle.querySelector('.sr-only').textContent = open ? 'Open navigation' : 'Close navigation';
      links.classList.toggle('is-open', !open);
    });

    links.querySelectorAll('a').forEach((link) => {
      link.addEventListener('click', closeNav);
    });

    document.addEventListener('keydown', (event) => {
      if (event.key === 'Escape') closeNav();
    });

    window.addEventListener('resize', () => {
      if (window.innerWidth > 700) closeNav();
    });

    document.addEventListener('click', (event) => {
      if (!nav || !links.classList.contains('is-open')) return;
      if (!nav.contains(event.target)) closeNav();
    });
  }

  if (header) {
    const updateHeader = () => header.classList.toggle('is-scrolled', window.scrollY > 8);
    updateHeader();
    window.addEventListener('scroll', updateHeader, { passive: true });
  }

  const dialog = document.getElementById('lightbox');
  const dialogImage = document.getElementById('lightbox-image');
  const dialogCaption = document.getElementById('lightbox-caption');
  const shots = document.querySelectorAll('.shot[data-full]');

  if (dialog && dialogImage && dialogCaption && shots.length) {
    let opener = null;

    const open = (button) => {
      opener = button;
      dialogImage.src = button.dataset.full;
      dialogImage.alt = button.dataset.caption || 'Veyra screenshot';
      dialogCaption.textContent = button.dataset.caption || '';
      document.body.classList.add('lightbox-open');

      if (typeof dialog.showModal === 'function') {
        dialog.showModal();
      } else {
        dialog.setAttribute('open', '');
      }
    };

    const close = () => {
      if (typeof dialog.close === 'function' && dialog.open) {
        dialog.close();
      } else {
        dialog.removeAttribute('open');
        document.body.classList.remove('lightbox-open');
      }

      if (opener) {
        opener.focus();
        opener = null;
      }
    };

    shots.forEach((button) => {
      button.addEventListener('click', () => open(button));
    });

    const closeButton = dialog.querySelector('.lightbox-close');
    if (closeButton) closeButton.addEventListener('click', close);

    dialog.addEventListener('click', (event) => {
      if (event.target === dialog) close();
    });

    dialog.addEventListener('close', () => {
      document.body.classList.remove('lightbox-open');
      if (opener) {
        opener.focus();
        opener = null;
      }
    });

    dialog.addEventListener('cancel', () => {
      document.body.classList.remove('lightbox-open');
    });
  }
})();
