# Huong Dan Chay HoaFoodAudio Bang Docker

Tai lieu nay duoc viet de giup anh hoac nguoi khac co the nhin mot lan la biet can lam gi de chay du an bang Docker.

No se tap trung vao 3 tinh huong thuc te nhat:

- chay du an tu dau tren may moi
- chay lai du an khi da co du lieu
- be nguyen du an sang may khac ma van giu duoc he thong

## 1. Docker trong du an nay dung de lam gi

Trong repo hien tai, Docker duoc dung cho 2 thanh phan:

- `db`: container PostgreSQL
- `backend`: container FastAPI

Phan web khong can container rieng, vi backend se mount thu muc `Web/` va phuc vu truc tiep cac trang:

- `/login`
- `/owner`
- `/superadmin`

Noi de hieu:

- PostgreSQL giu du lieu
- FastAPI doc du lieu tu PostgreSQL
- Web dashboard do FastAPI phuc vu
- App mobile goi API cua FastAPI

## 2. File Docker dang co trong repo

Nam trong:

- `Backend/docker-compose.yml`
- `Backend/Dockerfile`

`docker-compose.yml` dang tao 2 service:

### `db`
- image: `postgres:15-alpine`
- user: `admin`
- password: `password123`
- database: `food_street_db`
- port map ra may host: `5432:5432`

### `backend`
- build tu `Backend/Dockerfile`
- port map ra may host: `8000:8000`
- dung `DATABASE_URL=postgresql://admin:password123@db:5432/food_street_db`
- mount:
  - `Backend/` vao `/app`
  - `Web/` vao `/web`

Dieu nay co nghia la:

- backend trong container nhin thay code Python trong `Backend/`
- backend cung nhin thay giao dien web trong `Web/`

## 3. Dieu kien truoc khi chay

Anh can co:

- Docker Desktop tren Windows
hoac
- Docker Engine + Docker Compose tren Ubuntu/Linux

Kiem tra nhanh:

```powershell
docker --version
docker compose version
```

Neu 2 lenh nay chay duoc thi moi truong Docker co ban da san sang.

## 4. Cach chay lan dau tren may local

### Buoc 1: mo terminal tai thu muc Backend

Windows PowerShell:

```powershell
cd D:\C#\Backend
```

Ubuntu/Linux:

```bash
cd ~/CSharp/Backend
```

### Buoc 2: build va chay container

```powershell
docker compose up -d --build
```

Lenh nay se:

- tai image PostgreSQL neu may chua co
- build image backend tu `Dockerfile`
- tao container `foodstreet_db`
- tao container `foodstreet_backend`

### Buoc 3: kiem tra container da len chua

```powershell
docker ps
```

Anh can thay it nhat 2 container:

- `foodstreet_db`
- `foodstreet_backend`

### Buoc 4: nap schema vao database

Quan trong:

`docker compose` hien tai chi tao PostgreSQL rong, chu khong tu dong chay `schema.sql` va `migration.sql`.

Vay lan dau anh phai nap schema bang tay.

#### Cach lam tren Windows PowerShell

```powershell
Get-Content .\schema.sql | docker exec -i foodstreet_db psql -U admin -d food_street_db
Get-Content .\migration.sql | docker exec -i foodstreet_db psql -U admin -d food_street_db
```

#### Cach lam tren Ubuntu/Linux

```bash
docker exec -i foodstreet_db psql -U admin -d food_street_db < schema.sql
docker exec -i foodstreet_db psql -U admin -d food_street_db < migration.sql
```

### Buoc 5: mo he thong tren trinh duyet

Sau khi backend da len, mo:

- [http://localhost:8000/login](http://localhost:8000/login)
- [http://localhost:8000/owner](http://localhost:8000/owner)
- [http://localhost:8000/superadmin](http://localhost:8000/superadmin)

Neu vao duoc trang login la backend va web da len dung.

## 5. Tai khoan mac dinh de dang nhap web

Neu database moi va backend duoc phep seed admin mac dinh, he thong se tao:

- username: `admin`
- password: `admin123`

Neu login khong duoc, kha nang cao la:

- schema chua duoc nap
- migration chua duoc chay
- database dang la du lieu cu voi mat khau khac

## 6. Neu muon chay voi du lieu that thay vi database moi

Neu anh da co file dump du lieu that, vi du:

- `food_street_db_export.sql`
hoac
- `food_street_db_export.dump`

thi sau khi `docker compose up -d`, anh co the restore vao container DB.

### Truong hop file `.sql`

#### Windows PowerShell

```powershell
Get-Content .\food_street_db_export.sql | docker exec -i foodstreet_db psql -U admin -d food_street_db
```

#### Ubuntu/Linux

```bash
docker exec -i foodstreet_db psql -U admin -d food_street_db < food_street_db_export.sql
```

### Truong hop file `.dump`

Can copy file vao may host, sau do chay:

```powershell
docker cp .\food_street_db_export.dump foodstreet_db:/tmp/food_street_db_export.dump
docker exec -it foodstreet_db pg_restore -U admin -d food_street_db --clean --if-exists /tmp/food_street_db_export.dump
```

## 7. Cac lenh van hanh hay dung

### Xem log backend

```powershell
docker logs foodstreet_backend --tail 100
```

### Xem log realtime

```powershell
docker logs -f foodstreet_backend
```

### Xem log database

```powershell
docker logs foodstreet_db --tail 100
```

### Dung he thong

```powershell
docker compose down
```

### Dung he thong va xoa ca volume du lieu

Canh bao: lenh nay se xoa database trong volume Docker.

```powershell
docker compose down -v
```

### Khoi dong lai backend

```powershell
docker restart foodstreet_backend
```

### Khoi dong lai ca backend va db

```powershell
docker compose restart
```

## 8. Neu muon sua code backend va thay doi ngay

Vì `docker-compose.yml` dang mount thu muc `Backend/` vao `/app`, nen:

- anh sua code tren may host
- container backend nhin thay code moi

Tuy nhien, de backend nhan code moi chac chan nhat, anh nen restart:

```powershell
docker restart foodstreet_backend
```

Neu anh thay doi `requirements.txt` hoac sua `Dockerfile`, can build lai:

```powershell
docker compose up -d --build
```

## 9. App mobile co tu dong goi vao backend Docker local khong

Khong.

App MAUI hien tai dang mac dinh goi:

```text
https://hoafoodaudio.live
```

No nam trong:

- `App/ApiSettings.cs`

Neu anh muon app mobile goi vao backend Docker tren may local hoac may khac trong cung mang, anh phai:

1. sua `DefaultLanBaseUrl`
2. build lai app

Vi du:

```csharp
private const string DefaultLanBaseUrl = "http://192.168.1.10:8000";
```

Luu y:

- khong dung `localhost` neu dien thoai that dang goi toi may tinh
- phai dung IP LAN cua may tinh chay Docker

Vi du:

- may tinh: `192.168.1.10`
- app se goi: `http://192.168.1.10:8000`

## 10. Neu chi muon demo web va backend, khong can build app

Trong rat nhieu truong hop demo do an, anh chi can:

- chay Docker
- nap DB
- mo `/login`, `/owner`, `/superadmin`

Luc do khong can dong toi APK.

Neu muon demo day du mobile, moi can sua `ApiSettings.cs` va build lai APK.

## 11. Be nguyen do an nay sang may khac

Co 2 cach.

### Cach 1: Be source code, tao DB moi

Phu hop khi:

- may moi chua can du lieu that
- chi can he thong len duoc

Cac buoc:

1. copy repo hoac `git clone`
2. cai Docker
3. vao `Backend/`
4. chay:

```powershell
docker compose up -d --build
Get-Content .\schema.sql | docker exec -i foodstreet_db psql -U admin -d food_street_db
Get-Content .\migration.sql | docker exec -i foodstreet_db psql -U admin -d food_street_db
```

5. mo `http://localhost:8000/login`

### Cach 2: Be source code kem du lieu that

Phu hop khi:

- muon giu user, stall, analytics, request, review
- muon he thong tren may moi giong may cu

Cac buoc:

1. copy repo
2. copy them file dump DB
3. chay `docker compose up -d --build`
4. restore dump vao `foodstreet_db`
5. restart backend:

```powershell
docker restart foodstreet_backend
```

Neu anh dang dung QR da in san, nen giu nguyen `APP_SECRET`.

Neu doi `APP_SECRET`, ma QR cu co the khong resolve duoc nua.

## 12. Khi nao nen dung Docker, khi nao nen dung local Python

### Nen dung Docker khi:

- muon de moi truong tren may khac
- muon chay nhanh cho demo
- khong muon cai tung goi Python bang tay
- muon giu PostgreSQL tach rieng va de reset

### Nen dung local Python khi:

- dang debug nhanh backend
- muon sua code lien tuc va chay truc tiep
- da co PostgreSQL local san

Noi ngan gon:

- Docker hop cho demo, chuyen may, ban giao
- local Python hop cho dev hang ngay

## 13. Cach kiem tra he thong da len dung

Sau khi chay Docker, anh nen check 4 muc sau:

### Muc 1: container

```powershell
docker ps
```

### Muc 2: backend

Mo:

- [http://localhost:8000/login](http://localhost:8000/login)

### Muc 3: API

Mo:

- [http://localhost:8000/categories](http://localhost:8000/categories)

Neu thay JSON category tra ve la backend da chay.

### Muc 4: dashboard

Login roi vao:

- [http://localhost:8000/superadmin](http://localhost:8000/superadmin)

Neu thay dashboard va metrics thi toan bo he thong da thong.

## 14. Loi thuong gap

### Loi 1: backend len nhung login khong duoc

Thuong do:

- schema chua chay
- migration chua chay
- DB dang la volume cu

Cach xu ly:

```powershell
docker compose down -v
docker compose up -d --build
Get-Content .\schema.sql | docker exec -i foodstreet_db psql -U admin -d food_street_db
Get-Content .\migration.sql | docker exec -i foodstreet_db psql -U admin -d food_street_db
```

### Loi 2: web len duoc, app mobile khong lay du lieu

Thuong do:

- app van tro toi `https://hoafoodaudio.live`
- app chua build lai sau khi doi URL
- dien thoai khong goi duoc `localhost`

Cach xu ly:

- sua `App/ApiSettings.cs` thanh IP LAN
- build lai APK

### Loi 3: mat du lieu sau khi restart

Neu chi `docker restart` hoac `docker compose down`, du lieu van con.

Du lieu chi mat khi:

```powershell
docker compose down -v
```

vi lenh nay xoa ca volume PostgreSQL.

## 15. Goi y de nop bai hoac ban giao

Neu muon ban giao cho thay hoac cho may khac chay nhanh, goi nen co:

- source code repo
- `PRD.html`
- `PRD.docx`
- `Releases/HoaFoodAudio-v1.2.apk`
- file dump database that, neu can giu du lieu
- file nay: `HUONG_DAN_DOCKER.md`

## 16. Ket luan

Neu muc tieu cua anh la:

- len nhanh
- de chuyen may
- de demo
- giam loi moi truong

thi Docker la cach chay hop ly nhat cho backend va database cua HoaFoodAudio.

Con neu muc tieu la debug sau source moi ngay, co the chay local Python se thoang hon.

Nhung de ban giao do an, cach an toan nhat van la:

1. dung Docker cho `db` va `backend`
2. giu mot file dump DB that neu can du lieu
3. neu can demo app mobile thi sua `ApiSettings.cs` theo IP may chay Docker va build lai APK
