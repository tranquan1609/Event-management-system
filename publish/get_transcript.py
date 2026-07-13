import sys
import json
import logging
import re
import os
import subprocess
import tempfile
from typing import Optional, Dict, Any
import io

# Cấu hình logging: chỉ log ra stderr
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(sys.stderr)
    ]
)
logger = logging.getLogger(__name__)

# Đảm bảo stdout là UTF-8
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

print(json.dumps({"env": dict(os.environ)}, ensure_ascii=False), file=sys.stderr)

def validate_video_id(video_id: str) -> bool:
    """Kiểm tra tính hợp lệ của video ID"""
    if not video_id:
        logger.error("Empty video ID provided")
        return False
    # YouTube video ID thường có 11 ký tự
    is_valid = bool(re.match(r'^[a-zA-Z0-9_-]{11}$', video_id))
    if not is_valid:
        logger.error(f"Invalid video ID format: {video_id}")
    return is_valid

def convert_vtt_to_text(vtt_file: str) -> str:
    """Chuyển đổi file VTT thành text"""
    try:
        with open(vtt_file, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        text_lines = []
        previous_line = None
        
        for line in lines:
            # Bỏ qua các dòng header và timestamp
            if (line.startswith('WEBVTT') or 
                '-->' in line or 
                not line.strip() or
                line.startswith('Kind:') or
                line.startswith('Language:')):
                continue
            
            # Xử lý các thẻ HTML và timestamp
            clean_line = re.sub(r'<[^>]+>', '', line)  # Xóa thẻ HTML
            clean_line = re.sub(r'\d{2}:\d{2}:\d{2}\.\d{3}', '', clean_line)  # Xóa timestamp
            clean_line = re.sub(r'\[.*?\]', '', clean_line)  # Xóa text trong ngoặc vuông
            clean_line = re.sub(r'\(.*?\)', '', clean_line)  # Xóa text trong ngoặc đơn
            clean_line = re.sub(r'[^\w\s.,!?-]', '', clean_line)  # Chỉ giữ lại chữ cái, số và dấu câu cơ bản
            clean_line = clean_line.strip()
            
            # Bỏ qua dòng trùng lặp
            if clean_line and clean_line != previous_line:
                text_lines.append(clean_line)
                previous_line = clean_line
        
        # Nối các dòng và xử lý khoảng trắng
        text = ' '.join(text_lines)
        text = re.sub(r'\s+', ' ', text)  # Xóa khoảng trắng thừa
        text = re.sub(r'\s+([.,!?])', r'\1', text)  # Xóa khoảng trắng trước dấu câu
        text = re.sub(r'([.,!?])\s+', r'\1 ', text)  # Thêm khoảng trắng sau dấu câu
        
        return text.strip()
    except Exception as e:
        logger.error(f"Error converting VTT to text: {str(e)}")
        return ""

def get_transcript(video_id: str, lang: str = 'en') -> Optional[Dict[str, Any]]:
    """Lấy transcript từ video YouTube sử dụng yt-dlp"""
    if not validate_video_id(video_id):
        raise ValueError("Invalid YouTube video ID format")

    logger.info(f"Getting transcript for video ID: {video_id} in language: {lang}")
    
    # Tạo thư mục tạm để lưu phụ đề
    with tempfile.TemporaryDirectory() as temp_dir:
        try:
            # Chạy yt-dlp để tải phụ đề
            cmd = [
                'yt-dlp',
                '--write-auto-sub',
                '--sub-lang', lang,
                '--skip-download',
                '-o', f'{temp_dir}/{video_id}',
                f'https://www.youtube.com/watch?v={video_id}'
            ]
            
            logger.info(f"Running command: {' '.join(cmd)}")
            result = subprocess.run(cmd, capture_output=True, text=True)
            
            if result.returncode != 0:
                logger.error(f"yt-dlp error: {result.stderr}")
                raise Exception(f"Failed to download subtitles: {result.stderr}")
            
            # Tìm file .vtt trong thư mục tạm
            vtt_files = [f for f in os.listdir(temp_dir) if f.endswith(f'.{lang}.vtt')]
            if not vtt_files:
                logger.error(f"No .vtt file found for language: {lang}")
                raise Exception(f"No subtitle file found for language: {lang}")
            
            vtt_file = os.path.join(temp_dir, vtt_files[0])
            transcript_text = convert_vtt_to_text(vtt_file)
            
            if not transcript_text:
                raise Exception("Failed to convert VTT to text")
            
            return {
                "transcript": transcript_text,
                "language": lang,
                "metadata": {
                    "video_id": video_id,
                    "source": "yt-dlp",
                    "format": "vtt"
                }
            }
            
        except Exception as e:
            logger.error(f"Error getting transcript: {str(e)}")
            raise

def main():
    if len(sys.argv) < 2:
        error_msg = "Usage: python get_transcript.py <YouTubeVideoID> [language_code]"
        logger.error(error_msg)
        print(json.dumps({"error": error_msg}, ensure_ascii=False))
        sys.exit(1)

    video_id = sys.argv[1]
    lang = sys.argv[2] if len(sys.argv) > 2 else 'en'
    
    try:
        # Lấy transcript
        transcript_data = get_transcript(video_id, lang)
        
        if not transcript_data:
            error_msg = "Failed to get transcript data"
            logger.error(error_msg)
            print(json.dumps({"error": error_msg}, ensure_ascii=False))
            sys.exit(1)
        
        # In kết quả
        print(json.dumps(transcript_data, ensure_ascii=False))
        
    except ValueError as e:
        logger.error(f"Validation error: {str(e)}")
        print(json.dumps({"error": str(e)}, ensure_ascii=False))
        sys.exit(1)
    except Exception as e:
        error_msg = f"An unexpected error occurred: {str(e)}"
        logger.error(error_msg)
        print(json.dumps({"error": error_msg}, ensure_ascii=False))
        sys.exit(1)

if __name__ == "__main__":
    main()
