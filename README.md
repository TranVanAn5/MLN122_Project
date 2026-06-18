# MLN Realtime Game MVP

Web game quiz 10 nguoi theo format:

- Host tao phong va dieu khien tung vong.
- Nguoi choi nhap ten, ma phong, tra loi cau hoi realtime.
- He thong tinh diem, loai nguoi diem thap.
- Nguoi bi loai chuyen thanh nha dau tu.
- Host mo chung ket va cong bo nguoi chien thang.

## Cong nghe

- Backend: ASP.NET Core 9 Minimal API
- Realtime: Server-Sent Events (SSE) + REST actions
- Frontend: React 18 + HTML/CSS trong `Frontend`
- State: SQL Server qua Entity Framework Core

Phong, nguoi choi, cau tra loi, dau tu va diem hien duoc luu trong SQL Server. Restart server khong lam mat phong da tao.

## Chay local

```powershell
$env:DOTNET_CLI_HOME='C:\MLN122\.dotnet_home'
dotnet run --project Backend\GameServer.csproj --urls http://localhost:5127
```

Mo trinh duyet tai:

```text
http://localhost:5127
```

Frontend duoc render bang React 18 tu CDN va duoc backend ASP.NET Core serve truc tiep, nen khong can chay frontend dev server rieng.

## SQL Server va migration

Connection string nam trong:

- `Backend/appsettings.json`
- `Backend/appsettings.Development.json`

Mac dinh dang dung SQL Server tren port `1433`:

```text
Server=localhost,1433;Database=MLN122Db;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;Encrypt=False
```

Tao database bang migration:

```powershell
$env:DOTNET_CLI_HOME='C:\MLN122\.dotnet_home'
dotnet ef database update --project Backend\GameServer.csproj --startup-project Backend\GameServer.csproj
```

Neu dung SQL Server Express:

```text
Server=localhost\SQLEXPRESS;Database=MLN122Db;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False
```

Neu dung tai khoan SQL:

```text
Server=localhost,1433;Database=MLN122Db;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;Encrypt=False
```

Script SQL da duoc xuat tai `Backend/Data/Migrations/InitialCreate.sql`.

App tu dong chay migration khi khoi dong neu `Database:AutoMigrate` la `true`.

## Multiplayer/deploy

Frontend mo ket noi SSE toi:

```text
/api/rooms/{roomCode}/events
```

Moi hanh dong cua host/player ghi vao SQL Server, cac client dang o cung phong se nhan snapshot moi qua SSE. Khi deploy, can cau hinh bien moi truong:

```text
ConnectionStrings__DefaultConnection=Server=YOUR_SQL_HOST,1433;Database=MLN122Db;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;Encrypt=False
Database__AutoMigrate=true
```

Neu deploy nhieu instance app sau load balancer, SSE van doc lai state tu SQL Server. Ban nen dung sticky session hoac giam so instance khi chay game live de tranh ket noi SSE bi chuyen server lien tuc.

## Luong test nhanh

1. Mo `http://localhost:5127`, nhap ten host, bam **Tao phong**.
2. Mo them tab/an danh, nhap ten nguoi choi va ma phong.
3. Host bam **Bat dau cau hoi**.
4. Nguoi choi chon dap an.
5. Host bam **Khoa dap an**, sau do **Loai diem thap**.
6. Lap lai den khi host loai den khi chi con 1 nguoi, game mo **Dau tu**.
7. Nha dau tu dat diem cho nguoi chung ket theo tung cau hoi, toi da bang diem dang co.
8. Host bam **Mo chung ket solo**, sau do **Cau hoi chung ket** de dung bo cau hoi rieng.
9. Host khoa dap an. Neu dung, chia diem dau tu cua vong do ngay va quay lai dau tu cho vong tiep theo. Neu sai, game ket thuc ngay.

## Quy tac diem MVP

- Diem ban dau: 1000
- Vong 1: dung +100, sai 0
- Vong 2: dung +200, sai -50
- Vong 3+: dung +300, sai -100
- Dau tu theo tung cau hoi chung ket, toi da bang toan bo diem hien co
- Khi dau tu, diem dau tu duoc cong tam vao nguoi chung ket trong vong do
- Neu nguoi chung ket tra loi dung: nhan them 200 diem va giu 70% tong dau tu; nha dau tu nhan lai von + 30%
- Neu nguoi chung ket tra loi sai: nguoi chung ket mat phan diem duoc dau tu cua vong do; nha dau tu mat toan bo diem da dau tu

## Cau truc chinh

- `Frontend/index.html`: shell frontend va script React CDN.
- `Frontend/app.js`: React UI mot trang va cac lenh goi API.
- `Frontend/styles.css`: giao dien frontend, responsive layout va animation.
- `Backend/Program.cs`: cau hinh DI, database, static files va SignalR hub.
- `Backend/Presentation/Endpoints/GameEndpoints.cs`: REST API va SSE endpoints.
- `Backend/Presentation/Hubs/GameHub.cs`: cac lenh realtime tu host/player.
- `Backend/Presentation/Requests/GameRequests.cs`: request DTO cho API.
- `Backend/Business/Services/GameStateService.cs`: logic phong, diem, loai, dau tu, ket qua.
- `Backend/Business/Models/GameModels.cs`: room, player, question, answer, investment, snapshot.
- `Backend/Data`: EF Core DbContext, entities va migrations.
