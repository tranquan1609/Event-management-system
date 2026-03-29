// Khởi tạo biến để theo dõi tiến độ
let videoProgress = {
    lastSentProgress: 0, // Stores the last progress (accumulated watched time) successfully reported to the server
    duration: 0,
    lastUpdateTime: Date.now(), // Last time progress was actually sent to server
    updateInterval: 5000, // Cập nhật mỗi 5 giây
    videoId: null,
    attendeeId: null,
    player: null,
    trackingInterval: null,
    accumulatedWatchedTime: 0, // Total actual time user has watched
    lastPlayheadPosition: 0 // The player's actual time when we last accumulated
};

// Khởi tạo YouTube Player API
function initYouTubePlayer(videoId, containerId, attendeeId, initialViewDuration) {
    console.log('Initializing YouTube player for video:', videoId, 'container:', containerId, 'attendeeId:', attendeeId);
    
    // videoId ở đây là YouTube video ID, không phải database ID
    // Lưu YouTube video ID tạm thời để tạo player
    var youtubeVideoId = videoId;
    
    // Không override videoProgress.videoId nếu đã được set (có thể là database ID)
    if (!videoProgress.videoId || typeof videoProgress.videoId === 'string') {
        // Chỉ set nếu chưa có hoặc là string (YouTube ID cũ)
        // Database ID sẽ được set từ Details.cshtml
    }
    
    videoProgress.attendeeId = attendeeId;
    videoProgress.accumulatedWatchedTime = initialViewDuration || 0; // Initialize with loaded progress
    videoProgress.lastSentProgress = initialViewDuration || 0; // Also initialize lastSentProgress
    videoProgress.lastPlayheadPosition = initialViewDuration || 0; // Initialize lastPlayheadPosition
    
    if (typeof YT === 'undefined') {
        // Load YouTube IFrame API nếu chưa được load
        var tag = document.createElement('script');
        tag.src = "https://www.youtube.com/iframe_api";
        var firstScriptTag = document.getElementsByTagName('script')[0];
        firstScriptTag.parentNode.insertBefore(tag, firstScriptTag);
    }

    // Lưu lại hàm onYouTubeIframeAPIReady cũ nếu có
    var existingOnReady = window.onYouTubeIframeAPIReady;
    
    window.onYouTubeIframeAPIReady = function() {
        console.log('YouTube API ready, creating player for YouTube ID:', youtubeVideoId);
        
        // Gọi hàm cũ nếu có
        if (existingOnReady && typeof existingOnReady === 'function' && existingOnReady !== window.onYouTubeIframeAPIReady) {
            try {
                existingOnReady();
            } catch (e) {
                console.warn('Error calling existing onYouTubeIframeAPIReady:', e);
            }
        }
        
        try {
            videoProgress.player = new YT.Player(containerId, {
                videoId: youtubeVideoId,
                events: {
                    'onReady': onPlayerReady,
                    'onStateChange': onPlayerStateChange
                }
            });
            console.log('YouTube player created successfully');
        } catch (e) {
            console.error('Error creating YouTube player:', e);
        }
    };
    
    // Nếu API đã sẵn sàng, gọi ngay
    if (typeof YT !== 'undefined' && YT.Player) {
        console.log('YouTube API already loaded, creating player immediately');
        setTimeout(function() {
            try {
                videoProgress.player = new YT.Player(containerId, {
                    videoId: youtubeVideoId,
                    events: {
                        'onReady': onPlayerReady,
                        'onStateChange': onPlayerStateChange
                    }
                });
                console.log('YouTube player created successfully (immediate)');
            } catch (e) {
                console.error('Error creating YouTube player (immediate):', e);
            }
        }, 100);
    }
}

// Khởi tạo Facebook Video Player
function initFacebookPlayer(videoUrl, containerId, attendeeId, initialViewDuration) {
    videoProgress.videoId = videoUrl; // For Facebook, videoId might be the URL or a specific ID
    videoProgress.attendeeId = attendeeId;
    videoProgress.accumulatedWatchedTime = initialViewDuration || 0; // Initialize with loaded progress
    videoProgress.lastSentProgress = initialViewDuration || 0; // Also initialize lastSentProgress
    videoProgress.lastPlayheadPosition = initialViewDuration || 0; // Initialize lastPlayheadPosition

    FB.Event.subscribe('xfbml.ready', function(msg) {
        if (msg.type === 'video') {
            const player = msg.instance;
            videoProgress.player = player;
            
            player.getDuration(function(duration) {
                videoProgress.duration = duration;
                // For Facebook, seek directly after duration is known
                if (videoProgress.player.seek) {
                    videoProgress.player.seek(videoProgress.accumulatedWatchedTime);
                }
            });

            player.subscribe('startedPlaying', function() {
                videoProgress.lastPlayheadPosition = player.getCurrentPosition();
                startTracking();
            });

            player.subscribe('paused', function() {
                let actualPlayheadTime = player.getCurrentPosition();
                let elapsed = actualPlayheadTime - videoProgress.lastPlayheadPosition;
                const maxNormalElapsed = (videoProgress.updateInterval / 1000) * 1.5;
                console.log(`PAUSED (Facebook): currentPlayheadTime=${actualPlayheadTime.toFixed(2)}, lastPlayheadPosition=${videoProgress.lastPlayheadPosition.toFixed(2)}, elapsed=${elapsed.toFixed(2)}, maxNormalElapsed=${maxNormalElapsed.toFixed(2)}`);
                if (elapsed > 0 && elapsed <= maxNormalElapsed) {
                    videoProgress.accumulatedWatchedTime += elapsed;
                    console.log(`PAUSED (Facebook): Accumulated: ${videoProgress.accumulatedWatchedTime.toFixed(2)}`);
                }
                videoProgress.lastPlayheadPosition = actualPlayheadTime; // Update lastPlayheadPosition here
                stopTracking();
                updateProgress(false);
            });

            player.subscribe('finishedPlaying', function() {
                videoProgress.accumulatedWatchedTime = videoProgress.duration; 
                stopTracking();
                updateProgress(true);
            });
        }
    });
}

// Xử lý sự kiện YouTube Player
function onPlayerReady(event) {
    console.log('Player ready');
    videoProgress.duration = event.target.getDuration();
    
    // Cập nhật thanh tiến độ ban đầu
    if (videoProgress.accumulatedWatchedTime > 0 && videoProgress.duration > 0) {
        updateProgressBar(videoProgress.accumulatedWatchedTime, videoProgress.duration);
    }
    
    // Seek to initial progress if player is ready
    if (videoProgress.player.seekTo && videoProgress.accumulatedWatchedTime > 0) {
        videoProgress.player.seekTo(videoProgress.accumulatedWatchedTime, true); 
        showNotification(`Tiếp tục xem từ: ${(videoProgress.accumulatedWatchedTime / 60).toFixed(0)} phút ${(videoProgress.accumulatedWatchedTime % 60).toFixed(0)} giây`, 'info');
    }
}

function onPlayerStateChange(event) {
    // Trước khi xử lý, lấy thời gian hiện tại của player
    let currentPlayheadTime = 0;
    if (videoProgress.player && videoProgress.player.getCurrentTime) {
        currentPlayheadTime = videoProgress.player.getCurrentTime();
    } else if (videoProgress.player && videoProgress.player.getCurrentPosition) {
        currentPlayheadTime = videoProgress.player.getCurrentPosition();
    }

    switch(event.data) {
        case YT.PlayerState.PLAYING:
            console.log('Video started playing');
            // Khi bắt đầu phát, thiết lập lastPlayheadPosition là thời gian hiện tại của player
            // Điều này quan trọng để tính toán thời gian trôi qua chính xác từ điểm này.
            videoProgress.lastPlayheadPosition = currentPlayheadTime;
            startTracking();
            break;
        case YT.PlayerState.PAUSED:
            console.log('Video paused');
            stopTracking();
            if (videoProgress.player && videoProgress.player.getCurrentTime) {
                let actualPlayheadTime = videoProgress.player.getCurrentTime();
                let elapsed = actualPlayheadTime - videoProgress.lastPlayheadPosition;
                const maxNormalElapsed = (videoProgress.updateInterval / 1000) * 1.5;
                console.log(`PAUSED (YouTube): currentPlayheadTime=${actualPlayheadTime.toFixed(2)}, lastPlayheadPosition=${videoProgress.lastPlayheadPosition.toFixed(2)}, elapsed=${elapsed.toFixed(2)}, maxNormalElapsed=${maxNormalElapsed.toFixed(2)}`);
                if (elapsed > 0 && elapsed <= maxNormalElapsed) {
                    videoProgress.accumulatedWatchedTime += elapsed;
                    console.log(`PAUSED (YouTube): Accumulated: ${videoProgress.accumulatedWatchedTime.toFixed(2)}`);
                }
                videoProgress.lastPlayheadPosition = actualPlayheadTime; // Update lastPlayheadPosition here
            } else if (videoProgress.player && videoProgress.player.getCurrentPosition) {
                 let actualPlayheadTime = videoProgress.player.getCurrentPosition();
                 let elapsed = actualPlayheadTime - videoProgress.lastPlayheadPosition;
                 const maxNormalElapsed = (videoProgress.updateInterval / 1000) * 1.5;
                 console.log(`PAUSED (Facebook): currentPlayheadTime=${actualPlayheadTime.toFixed(2)}, lastPlayheadPosition=${videoProgress.lastPlayheadPosition.toFixed(2)}, elapsed=${elapsed.toFixed(2)}, maxNormalElapsed=${maxNormalElapsed.toFixed(2)}`);
                 if (elapsed > 0 && elapsed <= maxNormalElapsed) {
                    videoProgress.accumulatedWatchedTime += elapsed;
                    console.log(`PAUSED (Facebook): Accumulated: ${videoProgress.accumulatedWatchedTime.toFixed(2)}`);
                 }
                 videoProgress.lastPlayheadPosition = actualPlayheadTime; // Update lastPlayheadPosition here
            }
            updateProgress(false);
            break;
        case YT.PlayerState.ENDED:
            console.log('Video ended');
            stopTracking();
            videoProgress.accumulatedWatchedTime = videoProgress.duration; 
            updateProgress(true);
            break;
        case YT.PlayerState.BUFFERING:
            console.log('Video buffering');
            stopTracking();
            if (videoProgress.player && videoProgress.player.getCurrentTime) {
                let actualPlayheadTime = videoProgress.player.getCurrentTime();
                let elapsed = actualPlayheadTime - videoProgress.lastPlayheadPosition;
                const maxNormalElapsed = (videoProgress.updateInterval / 1000) * 1.5;
                console.log(`BUFFERING (YouTube): currentPlayheadTime=${actualPlayheadTime.toFixed(2)}, lastPlayheadPosition=${videoProgress.lastPlayheadPosition.toFixed(2)}, elapsed=${elapsed.toFixed(2)}, maxNormalElapsed=${maxNormalElapsed.toFixed(2)}`);
                if (elapsed > 0 && elapsed <= maxNormalElapsed) {
                    videoProgress.accumulatedWatchedTime += elapsed;
                    console.log(`BUFFERING (YouTube): Accumulated: ${videoProgress.accumulatedWatchedTime.toFixed(2)}`);
                }
                videoProgress.lastPlayheadPosition = actualPlayheadTime; // Update lastPlayheadPosition here
            } else if (videoProgress.player && videoProgress.player.getCurrentPosition) {
                 let actualPlayheadTime = videoProgress.player.getCurrentPosition();
                 let elapsed = actualPlayheadTime - videoProgress.lastPlayheadPosition;
                 const maxNormalElapsed = (videoProgress.updateInterval / 1000) * 1.5;
                 console.log(`BUFFERING (Facebook): currentPlayheadTime=${actualPlayheadTime.toFixed(2)}, lastPlayheadPosition=${videoProgress.lastPlayheadPosition.toFixed(2)}, elapsed=${elapsed.toFixed(2)}, maxNormalElapsed=${maxNormalElapsed.toFixed(2)}`);
                 if (elapsed > 0 && elapsed <= maxNormalElapsed) {
                    videoProgress.accumulatedWatchedTime += elapsed;
                    console.log(`BUFFERING (Facebook): Accumulated: ${videoProgress.accumulatedWatchedTime.toFixed(2)}`);
                 }
                 videoProgress.lastPlayheadPosition = actualPlayheadTime; // Update lastPlayheadPosition here
            }
            break;
    }
}

// Bắt đầu theo dõi tiến độ - đã sửa đổi để tích lũy thời gian xem thực tế
function startTracking() {
    console.log('Start tracking video progress...');
    if (videoProgress.trackingInterval) {
        clearInterval(videoProgress.trackingInterval);
    }
    
    if (!videoProgress.lastPlayheadPosition && videoProgress.player) {
         if (videoProgress.player.getCurrentTime) {
             videoProgress.lastPlayheadPosition = videoProgress.player.getCurrentTime();
         } else if (videoProgress.player.getCurrentPosition) {
             videoProgress.lastPlayheadPosition = videoProgress.player.getCurrentPosition();
         }
    }


    videoProgress.trackingInterval = setInterval(() => {
        if (videoProgress.player) {
            let currentPlayheadTime;
            if (videoProgress.player.getCurrentTime) { 
                currentPlayheadTime = videoProgress.player.getCurrentTime();
            } else if (videoProgress.player.getCurrentPosition) { 
                currentPlayheadTime = videoProgress.player.getCurrentPosition();
            } else {
                return;
            }

            let elapsed = currentPlayheadTime - videoProgress.lastPlayheadPosition;

            const maxNormalElapsed = (videoProgress.updateInterval / 1000) * 1.5; 
            
            console.log(`Tracking: currentPlayheadTime=${currentPlayheadTime.toFixed(2)}, lastPlayheadPosition=${videoProgress.lastPlayheadPosition.toFixed(2)}, elapsed=${elapsed.toFixed(2)}, maxNormalElapsed=${maxNormalElapsed.toFixed(2)}`);

            if (elapsed > 0 && elapsed <= maxNormalElapsed) {
                videoProgress.accumulatedWatchedTime += elapsed;
                videoProgress.lastPlayheadPosition = currentPlayheadTime;
                console.log(`Tracking: Accumulated: ${videoProgress.accumulatedWatchedTime.toFixed(2)}`);
            } else if (elapsed > maxNormalElapsed) {
                console.log(`Detected forward seek. Resetting lastPlayheadPosition to current. Old accumulated: ${videoProgress.accumulatedWatchedTime.toFixed(2)}`);
                videoProgress.lastPlayheadPosition = currentPlayheadTime;
                // Optionally, could update progress immediately to reflect the new seek position without adding to accumulated time
                // updateProgress(false); // Consider if this is desired behavior after a seek
            } else if (elapsed < 0) {
                console.log(`Detected backward seek. Resetting lastPlayheadPosition to current. Accumulated time unchanged. Old accumulated: ${videoProgress.accumulatedWatchedTime.toFixed(2)}`);
                videoProgress.lastPlayheadPosition = currentPlayheadTime;
            }
            
            // Cập nhật thanh tiến độ trong quá trình xem
            if (videoProgress.duration > 0) {
                const currentProgress = Math.max(videoProgress.accumulatedWatchedTime, videoProgress.lastSentProgress);
                updateProgressBar(currentProgress, videoProgress.duration);
            }
            
            updateProgress(false);

        }
    }, videoProgress.updateInterval);
}

// Dừng theo dõi tiến độ
function stopTracking() {
    console.log('Stop tracking video progress');
    if (videoProgress.trackingInterval) {
        clearInterval(videoProgress.trackingInterval);
    }
}

// Hiển thị thông báo
function showNotification(message, type = 'info') {
    // Tạo hoặc lấy container thông báo
    let container = document.getElementById('video-progress-notifications');
    if (!container) {
        container = document.createElement('div');
        container.id = 'video-progress-notifications';
        container.style.position = 'fixed';
        container.style.top = '20px';
        container.style.right = '20px';
        container.style.zIndex = '1000';
        document.body.appendChild(container);
    }

    // Tạo thông báo mới
    const notification = document.createElement('div');
    notification.className = `alert alert-${type} alert-dismissible fade show`;
    notification.innerHTML = `
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    `;

    // Thêm vào container
    container.appendChild(notification);

    // Tự động ẩn sau 5 giây
    setTimeout(() => {
        notification.classList.remove('show');
        setTimeout(() => notification.remove(), 300);
    }, 5000);
}

// Cập nhật tiến độ lên server - đã sửa đổi để gửi accumulatedWatchedTime
function updateProgress(isCompleted) {
    if (!videoProgress.player) {
        console.error('Player not initialized');
        return;
    }

    let finalProgressToSend;
    if (isCompleted) {
        finalProgressToSend = videoProgress.duration; // If completed, send full duration
    } else {
        // The progress to send should solely be the accumulated watched time.
        // It should never be less than what was last successfully sent,
        // unless it's a backward seek where accumulatedWatchedTime might be less than lastSentProgress (but we want to prevent sending less).
        // So, we take the maximum of accumulatedWatchedTime and lastSentProgress.
        finalProgressToSend = Math.max(Math.floor(videoProgress.accumulatedWatchedTime), videoProgress.lastSentProgress);
    }

    // Ensure final progress does not exceed total duration
    finalProgressToSend = Math.min(finalProgressToSend, videoProgress.duration);

    // Only send if the calculated progress is different from the last sent progress, or it's a completion event
    if (finalProgressToSend !== videoProgress.lastSentProgress || isCompleted) {
        // DO NOT update videoProgress.lastSentProgress here immediately.
        // It should only be updated AFTER a successful server response to prevent local state being out of sync.

        console.log('Sending progress:', {
            videoId: videoProgress.videoId,
            currentTime: finalProgressToSend,
            isCompleted: isCompleted,
            accumulatedWatchedTime: videoProgress.accumulatedWatchedTime, // For debugging
            duration: videoProgress.duration // For debugging
        });

        fetch('/Video/UpdateProgress', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify({
                videoId: videoProgress.videoId,
                currentTime: finalProgressToSend,
                isCompleted: isCompleted
            })
        })
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(data => {
            if (data.success) {
                // Cập nhật lastSentProgress sau khi server xác nhận thành công
                videoProgress.lastSentProgress = finalProgressToSend;
                
                // Cập nhật thanh tiến độ trên UI (không hiển thị thông báo cập nhật liên tục)
                updateProgressBar(finalProgressToSend, videoProgress.duration);
                
                // Chỉ hiển thị thông báo chúc mừng khi đạt yêu cầu
                if (data.isEligibleForCertificate) {
                    showNotification('🎉 Chúc mừng! Bạn đã đủ điều kiện nhận chứng nhận.', 'success');
                }
            }
             else {
                 // Kiểm tra nếu data.message tồn tại và hiển thị nó
                 showNotification(`Lỗi cập nhật tiến độ: ${data.message || 'Unknown error'}`, 'danger');
            }
        })
        .catch(error => {
            console.error('Error updating progress:', error);
            showNotification('Có lỗi khi cập nhật tiến độ. Vui lòng thử lại.', 'danger');
        });
    }
}

// Cập nhật thanh tiến độ trên UI
function updateProgressBar(watchedTime, totalDuration) {
    if (!totalDuration || totalDuration <= 0) return;
    
    const percentage = Math.min((watchedTime / totalDuration) * 100, 100);
    const progressBar = document.getElementById('progressBar');
    const progressText = document.getElementById('progressText');
    const progressPercentage = document.getElementById('progressPercentage');
    const watchedTimeElement = document.getElementById('watchedTime');
    
    if (progressBar) {
        progressBar.style.width = percentage.toFixed(1) + '%';
        progressBar.setAttribute('aria-valuenow', percentage.toFixed(1));
    }
    
    if (progressText) {
        progressText.textContent = percentage.toFixed(1) + '%';
    }
    
    if (progressPercentage) {
        progressPercentage.textContent = percentage.toFixed(1) + '%';
    }
    
    if (watchedTimeElement) {
        watchedTimeElement.textContent = Math.floor(watchedTime);
    }
}

// Thêm trình nghe sự kiện khi trang bị dỡ tải (chuyển trang)
window.addEventListener('beforeunload', () => {
    console.log('Page is unloading, sending final progress.');
    sendFinalProgressOnUnload();
});

function sendFinalProgressOnUnload() {
    if (!videoProgress.player || !videoProgress.videoId) {
        return; // Không có gì để gửi
    }
    
    // Thực hiện một lần tích lũy cuối cùng trước khi gửi
    let currentPlayheadTime = 0;
    if (videoProgress.player.getCurrentTime) {
        currentPlayheadTime = videoProgress.player.getCurrentTime();
    } else if (videoProgress.player.getCurrentPosition) {
        currentPlayheadTime = videoProgress.player.getCurrentPosition();
    }

    let elapsed = currentPlayheadTime - videoProgress.lastPlayheadPosition;
    const maxNormalElapsed = (videoProgress.updateInterval / 1000) * 1.5;
    if (elapsed > 0 && elapsed <= maxNormalElapsed) {
        videoProgress.accumulatedWatchedTime += elapsed;
    }

    const timeToSend = Math.floor(videoProgress.accumulatedWatchedTime);
    const finalProgressToSend = Math.min(Math.max(timeToSend, videoProgress.lastSentProgress), videoProgress.duration);

    // Sử dụng sendBeacon để gửi đáng tin cậy khi trang bị dỡ tải
    if (navigator.sendBeacon) {
        console.log('Sending final progress via sendBeacon:', finalProgressToSend);
        const blob = new Blob([JSON.stringify({
            videoId: videoProgress.videoId,
            currentTime: finalProgressToSend,
            isCompleted: false // Giả sử chưa hoàn thành khi dỡ tải, trừ khi thực sự đạt đến cuối
        })], {type : 'application/json'});
        navigator.sendBeacon('/Video/UpdateProgress', blob);
    } else {
        // Giải pháp thay thế cho các trình duyệt cũ hơn: XHR đồng bộ (không khuyến khích)
        console.warn('sendBeacon not supported. Attempting synchronous XHR for final progress.');
        const xhr = new XMLHttpRequest();
        xhr.open('POST', '/Video/UpdateProgress', false); // Đồng bộ
        xhr.setRequestHeader('Content-Type', 'application/json');
        xhr.setRequestHeader('RequestVerificationToken', document.querySelector('input[name="__RequestVerificationToken"]').value);
        xhr.send(JSON.stringify({
            videoId: videoProgress.videoId,
            currentTime: finalProgressToSend,
            isCompleted: false
        }));
    }
} 