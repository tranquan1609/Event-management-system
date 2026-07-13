// Sakura.js – hiệu ứng hoa anh đào đẹp mắt với nhiều màu sắc và animation mượt mà

window.addEventListener("load", function () {
    const canvas = document.getElementById("sakura");
    if (!canvas) return;

    const ctx = canvas.getContext("2d");
    const petals = [];
    const petalCount = 80; // Tăng số lượng cánh hoa

    // Màu sắc đa dạng cho cánh hoa
    const petalColors = [
        { r: 255, g: 182, b: 193 }, // Pink
        { r: 255, 192, 203 }, // Light Pink
        { r: 255, 160, 122 }, // Light Salmon
        { r: 255, 105, 180 }, // Hot Pink
        { r: 255, 20, 147 }, // Deep Pink
        { r: 255, 182, 193 }, // Pink
        { r: 240, 128, 128 }, // Light Coral
        { r: 255, 218, 185 }, // Peach Puff
    ];

    function random(min, max) {
        return Math.random() * (max - min) + min;
    }

    function Petal() {
        this.reset();
    }

    Petal.prototype.reset = function () {
        this.x = random(0, canvas.width);
        this.y = random(-canvas.height * 0.5, 0);
        this.r = random(2, 5); // Kích thước đa dạng hơn
        this.d = random(0.8, 2.5); // Tốc độ rơi
        this.tilt = random(-Math.PI / 6, Math.PI / 6); // Góc nghiêng
        this.tiltAngleIncrement = random(0.01, 0.03); // Tốc độ xoay
        this.tiltAngle = 0;
        this.opacity = random(0.4, 0.95);
        this.rotation = random(0, Math.PI * 2); // Góc xoay ban đầu
        this.rotationSpeed = random(0.01, 0.03); // Tốc độ xoay
        this.wind = random(-0.5, 0.5); // Hiệu ứng gió
        this.windSpeed = random(0.01, 0.02);
        this.windDirection = random(0, Math.PI * 2);
        
        // Chọn màu ngẫu nhiên
        const colorIndex = Math.floor(random(0, petalColors.length));
        const color = petalColors[colorIndex];
        this.color = `rgba(${color.r}, ${color.g}, ${color.b}, ${this.opacity})`;
    };

    Petal.prototype.draw = function () {
        ctx.save();
        
        // Di chuyển đến vị trí bông hoa
        ctx.translate(this.x, this.y);
        
        // Xoay bông hoa
        ctx.rotate(this.rotation);
        
        const petalSize = this.r;
        const centerX = 0;
        const centerY = 0;
        
        // Vẽ bông hoa sakura 5 cánh
        ctx.beginPath();
        
        // Vẽ 5 cánh hoa
        for (let i = 0; i < 5; i++) {
            const angle = (i * Math.PI * 2) / 5 - Math.PI / 2; // Bắt đầu từ trên
            const petalX = centerX + Math.cos(angle) * petalSize;
            const petalY = centerY + Math.sin(angle) * petalSize;
            
            if (i === 0) {
                ctx.moveTo(petalX, petalY);
            } else {
                ctx.lineTo(petalX, petalY);
            }
            
            // Vẽ đường cong cho cánh hoa (tạo hình trái tim)
            const controlX1 = centerX + Math.cos(angle) * petalSize * 0.5;
            const controlY1 = centerY + Math.sin(angle) * petalSize * 0.5;
            const controlX2 = centerX + Math.cos(angle + Math.PI / 5) * petalSize * 0.3;
            const controlY2 = centerY + Math.sin(angle + Math.PI / 5) * petalSize * 0.3;
            const nextAngle = ((i + 1) * Math.PI * 2) / 5 - Math.PI / 2;
            const nextPetalX = centerX + Math.cos(nextAngle) * petalSize;
            const nextPetalY = centerY + Math.sin(nextAngle) * petalSize;
            
            ctx.bezierCurveTo(controlX1, controlY1, controlX2, controlY2, nextPetalX, nextPetalY);
        }
        
        ctx.closePath();
        
        // Gradient màu cho bông hoa
        const gradient = ctx.createRadialGradient(centerX, centerY, 0, centerX, centerY, petalSize * 1.5);
        gradient.addColorStop(0, this.color);
        gradient.addColorStop(0.6, this.color.replace(/[\d\.]+\)$/g, (this.opacity * 0.8).toFixed(2) + ')'));
        gradient.addColorStop(1, this.color.replace(/[\d\.]+\)$/g, '0)'));
        
        ctx.fillStyle = gradient;
        ctx.fill();
        
        // Thêm viền cho bông hoa
        ctx.strokeStyle = this.color.replace(/[\d\.]+\)$/g, (this.opacity * 0.4).toFixed(2) + ')');
        ctx.lineWidth = 0.8;
        ctx.stroke();
        
        // Vẽ nhụy hoa ở giữa (màu vàng nhạt)
        ctx.beginPath();
        ctx.arc(centerX, centerY, petalSize * 0.25, 0, Math.PI * 2);
        const centerGradient = ctx.createRadialGradient(centerX, centerY, 0, centerX, centerY, petalSize * 0.25);
        centerGradient.addColorStop(0, `rgba(255, 255, 200, ${this.opacity * 0.9})`);
        centerGradient.addColorStop(1, `rgba(255, 220, 150, ${this.opacity * 0.6})`);
        ctx.fillStyle = centerGradient;
        ctx.fill();
        
        // Vẽ các chấm nhỏ trong nhụy hoa
        ctx.fillStyle = `rgba(255, 200, 100, ${this.opacity * 0.8})`;
        for (let i = 0; i < 5; i++) {
            const dotAngle = (i * Math.PI * 2) / 5;
            const dotX = centerX + Math.cos(dotAngle) * petalSize * 0.15;
            const dotY = centerY + Math.sin(dotAngle) * petalSize * 0.15;
            ctx.beginPath();
            ctx.arc(dotX, dotY, petalSize * 0.08, 0, Math.PI * 2);
            ctx.fill();
        }
        
        // Thêm highlight (ánh sáng) trên một cánh hoa
        ctx.beginPath();
        const highlightAngle = -Math.PI / 2; // Cánh hoa trên cùng
        const highlightX = centerX + Math.cos(highlightAngle) * petalSize * 0.4;
        const highlightY = centerY + Math.sin(highlightAngle) * petalSize * 0.4;
        ctx.arc(highlightX, highlightY, petalSize * 0.2, 0, Math.PI * 2);
        ctx.fillStyle = `rgba(255, 255, 255, ${this.opacity * 0.4})`;
        ctx.fill();
        
        ctx.restore();
    };

    Petal.prototype.update = function () {
        // Cập nhật vị trí
        this.y += this.d;
        
        // Hiệu ứng gió (swaying)
        this.windDirection += this.windSpeed;
        this.x += Math.sin(this.windDirection) * this.wind;
        
        // Xoay cánh hoa
        this.rotation += this.rotationSpeed;
        this.tiltAngle += this.tiltAngleIncrement;
        this.tilt = Math.sin(this.tiltAngle) * (Math.PI / 6);
        
        // Thêm chuyển động ngẫu nhiên nhẹ
        this.x += Math.sin(this.y * 0.01) * 0.3;
        
        // Reset khi rơi ra ngoài màn hình
        if (this.y > canvas.height + 10) {
            this.reset();
            this.y = -10;
        }
        
        // Reset khi ra ngoài biên trái/phải
        if (this.x < -10) {
            this.x = canvas.width + 10;
        } else if (this.x > canvas.width + 10) {
            this.x = -10;
        }
    };

    function draw() {
        // Clear với fade effect nhẹ để tạo trail
        ctx.fillStyle = 'rgba(255, 255, 255, 0.05)';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        
        // Vẽ tất cả cánh hoa
        for (let i = 0; i < petals.length; i++) {
            petals[i].draw();
            petals[i].update();
        }
        
        requestAnimationFrame(draw);
    }

    function resizeCanvas() {
        canvas.width = window.innerWidth;
        canvas.height = window.innerHeight;
        
        // Đảm bảo cánh hoa không bị mất khi resize
        petals.forEach(petal => {
            if (petal.x > canvas.width) petal.x = canvas.width;
            if (petal.y > canvas.height) petal.y = canvas.height;
        });
    }

    // Khởi tạo
    resizeCanvas();
    window.addEventListener("resize", resizeCanvas);

    // Tạo cánh hoa với delay để tạo hiệu ứng rơi tự nhiên
    for (let i = 0; i < petalCount; i++) {
        const petal = new Petal();
        petal.y = random(-canvas.height, canvas.height * 0.5); // Rải đều trên màn hình
        petals.push(petal);
    }

    // Bắt đầu animation
    draw();
});
