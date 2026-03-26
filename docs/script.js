// Fetch latest release from GitHub API
document.addEventListener('DOMContentLoaded', () => {
    // REplace with your actual GitHub username and repository name
    const GITHUB_USERNAME = 'Mujqk';
    const GITHUB_REPO = 'SayurinOwnChecker';
    
    const downloadBtn = document.getElementById('download-btn');
    const versionBadge = document.getElementById('version-badge');

    // Only attempt fetch if the user places actual repository info
    if (GITHUB_USERNAME !== 'YOUR_USERNAME') {
        fetch(`https://api.github.com/repos/${GITHUB_USERNAME}/${GITHUB_REPO}/releases/latest`)
            .then(response => response.json())
            .then(data => {
                if (data.tag_name && data.assets.length > 0) {
                    const version = data.tag_name;
                    const downloadUrl = data.assets[0].browser_download_url; // Assuming the first asset is the .exe/.zip
                    
                    versionBadge.textContent = `${version} Доступна`;
                    downloadBtn.href = downloadUrl;
                    
                    // Add smooth update animation
                    downloadBtn.style.animation = 'pulse 1s';
                }
            })
            .catch(error => console.error('Error fetching release:', error));
    } else {
        // Fallback or local dev
        downloadBtn.href = '#';
        downloadBtn.addEventListener('click', (e) => {
            e.preventDefault();
            alert('Чтобы кнопка скачивания работала, замените YOUR_USERNAME и YOUR_REPO в файле script.js на ваши данные GitHub.');
        });
    }

    // Smooth scrolling for anchor links
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            e.preventDefault();
            document.querySelector(this.getAttribute('href')).scrollIntoView({
                behavior: 'smooth'
            });
        });
    });
});
