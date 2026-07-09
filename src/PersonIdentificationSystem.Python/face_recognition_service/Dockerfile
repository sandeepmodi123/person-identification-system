FROM python:3.10-slim

WORKDIR /app

# System dependencies for OpenCV
RUN apt-get update && apt-get install -y --no-install-recommends \
    libglib2.0-0 libsm6 libxext6 libxrender-dev libgomp1 \
    && rm -rf /var/lib/apt/lists/*

COPY face_recognition_service/requirements.txt /tmp/requirements.face.txt
COPY stream_processor/requirements.txt /tmp/requirements.stream.txt
RUN pip install --no-cache-dir -r /tmp/requirements.face.txt -r /tmp/requirements.stream.txt

COPY face_recognition_service /app/face_recognition_service
COPY stream_processor /app/stream_processor

RUN mkdir -p /app/models

EXPOSE 8000
EXPOSE 8085

WORKDIR /app

CMD ["uvicorn", "face_recognition_service.main:app", "--host", "0.0.0.0", "--port", "8000"]
