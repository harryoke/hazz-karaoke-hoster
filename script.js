document.documentElement.classList.add('js');
const button=document.querySelector('.menu'),nav=document.querySelector('header nav');
button?.addEventListener('click',()=>button.setAttribute('aria-expanded',String(button.getAttribute('aria-expanded')!=='true')));
nav?.querySelectorAll('a').forEach(a=>a.addEventListener('click',()=>button?.setAttribute('aria-expanded','false')));
