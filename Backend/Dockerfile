FROM python:3.9-slim

# Cài đặt các thư viện hệ thống cần thiết cho psycopg2
RUN apt-get update && apt-get install -y libpq-dev gcc && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# Chạy server tại port 8000
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]