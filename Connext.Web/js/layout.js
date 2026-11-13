document.querySelectorAll('[data-include]').forEach(el => {
    fetch(el.dataset.include)
        .then(res => res.text())
        .then(html => {
            el.outerHTML = html;
            markActiveLink();
            if (typeof onLayoutLoaded === 'function') onLayoutLoaded();
        });
});

function markActiveLink() {
    let page = location.pathname.split('/').pop();
    if (page === 'home.html') page = 'chat.html';
    document.querySelectorAll('.sidebar-link').forEach(link => {
        if (link.getAttribute('href') === page) {
            link.classList.add('active');
        }
    });
}