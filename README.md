# HoaFoodAudio

README nay chi gom 3 viec:

- chay backend bang Docker
- chay voi du lieu co ban hoac du lieu gian hang va user co san
- bien laptop thanh server local de may khac trong cung mang goi vao

## 1. Backend Docker trong du an nay la gi

Du an dang dung 2 container:

- `foodstreet_db`: PostgreSQL
- `foodstreet_backend`: FastAPI

Backend se phuc vu ca API lan cac trang web trong thu muc `Web/`.
Chi can backend len la co the mo:

- `http://localhost:8000/login`
- `http://localhost:8000/owner`
- `http://localhost:8000/superadmin`

File Docker dang dung:

- `Backend/docker-compose.yml`
- `Backend/Dockerfile`

## 2. Dieu kien truoc khi chay

Can co:

- Docker Desktop tren Windows
hoac
- Docker Engine + Docker Compose tren Ubuntu/Linux

Kiem tra nhanh:

```powershell
docker --version
docker compose version
```

## 3. Chay backend voi du lieu co ban

Day la cach chay nhanh nhat khi muon len he thong moi.

Luu y quan trong:

Repo hien tai khong con file seed stall mau rieng.
Che do "du lieu co ban" chi tao:

- schema bang
- roles
- languages
- categories
- tai khoan `admin`

Nghia la sau buoc nay anh co backend chay duoc, co dang nhap admin, nhung khong phai day du stall va user that.

### Buoc 1: vao thu muc backend

```powershell
cd D:\C#\Backend
```

### Buoc 2: build va chay container

```powershell
docker compose up -d --build
```

### Buoc 3: nap schema va migration vao DB

```powershell
Get-Content .\schema.sql | docker exec -i foodstreet_db psql -U admin -d food_street_db
Get-Content .\migration.sql | docker exec -i foodstreet_db psql -U admin -d food_street_db
```

### Buoc 4: mo backend

Mo trinh duyet:

- [http://localhost:8000/login](http://localhost:8000/login)
- [http://localhost:8000/categories](http://localhost:8000/categories)

Tai khoan mac dinh:

- username: `admin`
- password: `admin123`

## 4. Chay backend voi du lieu gian hang va user co san

Neu muon backend len kem thong tin gian hang, owner, user va cac du lieu san co, dung file export DB.

Trong may local cua anh hien da co file:

- `Backend/food_street_db_export.sql`

File nay khong dua len Git de tranh qua nang va tranh lo du lieu that.
Neu be sang may khac, phai copy file nay di kem repo.

### Cach nap du lieu co san tu file SQL

### Buoc 1: vao thu muc backend

```powershell
cd D:\C#\Backend
```

### Buoc 2: chay Docker

```powershell
docker compose up -d --build
```

### Buoc 3: xoa schema cu trong container DB

Lenh nay dung khi muon thay toan bo DB bang bo du lieu export:

```powershell
docker exec -i foodstreet_db psql -U admin -d food_street_db -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
```

### Buoc 4: restore file export

```powershell
Get-Content .\food_street_db_export.sql | docker exec -i foodstreet_db psql -U admin -d food_street_db
```

### Buoc 5: chay migration de cap nhat cau truc moi nhat

```powershell
Get-Content .\migration.sql | docker exec -i foodstreet_db psql -U admin -d food_street_db
```

### Buoc 6: restart backend

```powershell
docker restart foodstreet_backend
```

Luc nay backend se co day du hon:

- categories
- gian hang
- owner
- user
- reviews
- listening logs
- location logs

Neu anh co file `.dump` thay vi `.sql`, co the restore bang:

```powershell
docker cp .\food_street_db_export.dump foodstreet_db:/tmp/food_street_db_export.dump
docker exec -it foodstreet_db pg_restore -U admin -d food_street_db --clean --if-exists /tmp/food_street_db_export.dump
docker restart foodstreet_backend
```

## 5. Bien laptop thanh server local

Muc tieu cua phan nay la de:

- mo web tren may khac trong cung Wi-Fi hoac cung mang LAN
- cho dien thoai hoac may khac goi API vao laptop

`docker-compose.yml` da map san:

- `8000:8000` cho backend
- `5432:5432` cho PostgreSQL

Vi vay chi can container dang chay la laptop da co the dong vai tro local server.

### Buoc 1: tim IP LAN cua laptop

Windows PowerShell:

```powershell
ipconfig
```

Tim dong `IPv4 Address`, vi du:

```text
192.168.1.10
```

### Buoc 2: giu backend dang chay

```powershell
cd D:\C#\Backend
docker compose up -d --build
```

### Buoc 3: mo tu may khac trong cung mang

Tren may khac, mo:

- `http://192.168.1.10:8000/login`
- `http://192.168.1.10:8000/categories`

Neu vao duoc thi laptop da dang chay nhu local server.

### Buoc 4: neu Windows chan cong 8000

Neu may khac khong vao duoc du IP dung, kiem tra:

- laptop va thiet bi khac co cung mang hay khong
- Windows Firewall co chan cong `8000` hay khong

Neu can, mo inbound rule cho TCP port `8000`.

## 6. Neu app Android muon goi vao laptop local

Backend da co the chay local server, nhung app MAUI hien tai khong tu dong doi sang IP LAN.

No dang mac dinh goi:

```text
https://hoafoodaudio.live
```

No nam o file:

- `App/ApiSettings.cs`

Neu muon app tren dien thoai goi vao laptop trong cung mang, sua:

```csharp
private const string DefaultLanBaseUrl = "http://192.168.1.10:8000";
```

Sau do build lai APK.

Luu y:

- khong dung `localhost` neu dien thoai that dang goi toi laptop
- phai dung IP LAN cua laptop
- laptop va dien thoai phai cung Wi-Fi hoac cung mang noi bo

## 7. Cac lenh kiem tra nhanh

### Xem container dang chay

```powershell
docker ps
```

### Xem log backend

```powershell
docker logs foodstreet_backend --tail 100
```

### Xem log realtime

```powershell
docker logs -f foodstreet_backend
```

### Restart backend

```powershell
docker restart foodstreet_backend
```

### Dung he thong

```powershell
docker compose down
```

### Xoa ca volume DB

Canh bao: lenh nay xoa database trong Docker.

```powershell
docker compose down -v
```

## 8. Be nguyen du an sang may khac

Neu may khac chi can backend co ban:

1. copy repo hoac `git clone`
2. cai Docker
3. vao `Backend`
4. chay:

```powershell
docker compose up -d --build
Get-Content .\schema.sql | docker exec -i foodstreet_db psql -U admin -d food_street_db
Get-Content .\migration.sql | docker exec -i foodstreet_db psql -U admin -d food_street_db
```

Neu may khac can day du gian hang va user:

1. copy repo
2. copy them `Backend/food_street_db_export.sql` hoac `.dump`
3. chay Docker
4. restore DB export
5. restart backend

## 9. Loi thuong gap

### Loi 1: vao duoc container nhung login khong duoc

Thuong do:

- chua chay `schema.sql`
- chua chay `migration.sql`
- DB dang la volume cu

Cach xu ly:

```powershell
docker compose down -v
docker compose up -d --build
Get-Content .\schema.sql | docker exec -i foodstreet_db psql -U admin -d food_street_db
Get-Content .\migration.sql | docker exec -i foodstreet_db psql -U admin -d food_street_db
```

### Loi 2: web local len duoc nhung dien thoai khong vao duoc

Thuong do:

- dung sai IP LAN
- khac mang Wi-Fi
- firewall chan cong `8000`

### Loi 3: app mobile van lay server online

Thuong do:

- chua sua `App/ApiSettings.cs`
- chua build lai APK
- van dang cai ban APK cu

## 10. Tom tat ngan

Neu anh muon chay nhanh de demo:

- backend co ban: `schema.sql` + `migration.sql`
- backend day du gian hang va user: restore `food_street_db_export.sql`
- laptop thanh local server: giu Docker chay, lay IP LAN, mo cong `8000`
