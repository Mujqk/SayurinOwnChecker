// Fetch latest release from GitHub API
document.addEventListener('DOMContentLoaded', () => {
    const GITHUB_USERNAME = 'Mujqk';
    const GITHUB_REPO = 'SayurinOwnChecker';
    
    const downloadBtn = document.getElementById('download-btn');
    const versionBadge = document.getElementById('version-badge');

    if (GITHUB_USERNAME && GITHUB_USERNAME !== 'YOUR_USERNAME') {
        fetch(`https://api.github.com/repos/${GITHUB_USERNAME}/${GITHUB_REPO}/releases/latest`)
            .then(response => response.json())
            .then(data => {
                if (data.tag_name) {
                    const version = data.tag_name;
                    versionBadge.textContent = `${version} Доступна`;
                    
                    if (data.assets && data.assets.length > 0) {
                        const downloadUrl = data.assets[0].browser_download_url;
                        downloadBtn.href = downloadUrl;
                        
                        if (downloadBtn.innerHTML.includes('Скачать')) {
                             const icon = downloadBtn.querySelector('i');
                             downloadBtn.innerHTML = '';
                             if (icon) downloadBtn.appendChild(icon);
                             downloadBtn.innerHTML += ` Скачать ${version}`;
                        }
                    }
                }
            })
            .catch(error => console.error('Error fetching release:', error));
    }

    // Slider Logic for Hero Mockup
    const slides = document.querySelectorAll('.slider-img');
    const dots = document.querySelectorAll('.slider-dots .dot');
    const prevBtn = document.getElementById('prev-slide');
    const nextBtn = document.getElementById('next-slide');
    let currentSlide = 0;

    function showSlide(index) {
        slides.forEach(slide => slide.classList.remove('active'));
        dots.forEach(dot => dot.classList.remove('active'));
        
        slides[index].classList.add('active');
        dots[index].classList.add('active');
    }

    if (prevBtn && nextBtn) {
        prevBtn.addEventListener('click', () => {
            currentSlide = (currentSlide > 0) ? currentSlide - 1 : slides.length - 1;
            showSlide(currentSlide);
        });

        nextBtn.addEventListener('click', () => {
            currentSlide = (currentSlide < slides.length - 1) ? currentSlide + 1 : 0;
            showSlide(currentSlide);
        });

        dots.forEach((dot, index) => {
            dot.addEventListener('click', () => {
                currentSlide = index;
                showSlide(currentSlide);
            });
        });

        // Auto-play
        setInterval(() => {
            currentSlide = (currentSlide < slides.length - 1) ? currentSlide + 1 : 0;
            showSlide(currentSlide);
        }, 5000);
    }

    // Smooth scrolling for anchor links
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            const targetId = this.getAttribute('href');
            if (targetId === '#') return;
            
            const targetElement = document.querySelector(targetId);
            if (targetElement) {
                e.preventDefault();
                targetElement.scrollIntoView({
                    behavior: 'smooth'
                });
            }
        });
    });
});
