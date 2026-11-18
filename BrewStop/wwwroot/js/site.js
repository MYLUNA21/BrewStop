// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener('submit', function (e) {
    var f = e.target && e.target.closest ? e.target.closest('form[data-fav-form]') : null;
    if (!f || !window.fetch) return;

    e.preventDefault();
    var btn = f.querySelector('button');
    if (btn) btn.disabled = true;

    fetch(f.action, {
        method: 'POST',
        body: new FormData(f),   // includes the antiforgery token field
        headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' },
        credentials: 'same-origin'
    })
        .then(function (r) {
            if (!r.ok || r.redirected) throw new Error('toggle failed');
            return r.json();
        })
        .then(function (d) {
            var icon = btn.querySelector('i');
            icon.className = (d.saved ? 'fa-solid' : 'fa-regular') + ' fa-heart';
            btn.classList.toggle('text-rose-400', d.saved);
            btn.classList.toggle('text-white', !d.saved);
            btn.setAttribute('aria-pressed', d.saved ? 'true' : 'false');
            btn.disabled = false;
        })
        .catch(function () { f.submit(); });
});