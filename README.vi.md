# Samurai Shodown II — Online

*[English](README.md) · Tiếng Việt*

Chơi Samurai Shodown II qua internet với rollback netcode, ghép trận tự động và
đục lỗ NAT — không bên nào phải mở cổng trên router.

**Kiến trúc, giao thức và tình trạng hiện tại: [`ARCHITECTURE.vi.md`](ARCHITECTURE.vi.md).**

> Kho này **chỉ chứa engine và công cụ**. Samurai Shodown II vẫn thuộc sở hữu
> của SNK và vẫn đang được bán; bạn phải tự có bản game của mình. Cùng mô hình
> với ScummVM và OpenRA.

---

## Bắt đầu nhanh

```bash
git clone https://github.com/thanhnhu/SamuraiOnline.git
cd SamuraiOnline
./setup.sh --chars
```

`setup.sh` sinh nhân vật rồi build. Sau đó:

```bash
cd Ikemen-GO
./Ikemen_GO_Linux                                          # Linux
DISPLAY=:0 WAYLAND_DISPLAY=wayland-0 \
  XDG_RUNTIME_DIR=/mnt/wslg/runtime-dir ./Ikemen_GO_Linux  # WSLg
```

### `setup.sh` làm gì, và vì sao cần nó

Engine **và toàn bộ asset chạy game** đều nằm sẵn trong kho này, nên chỉ cần
clone là build và chạy được. Ikemen GO gốc để asset ở kho riêng; bản fork này
thì không, vì một bản clone tự chứa đủ có giá trị hơn một bản clone nhỏ gọn.
Asset theo giấy phép CC BY 3.0 và CC BY-NC 3.0 — xem `Ikemen-GO/LICENCE.txt`.

Nhân vật là ngoại lệ. `jubei` và `jubei2` được sinh ra từ tranh vẽ của Samurai
Shodown, thứ không thuộc quyền phân phối của chúng ta, nên `--chars` sẽ clone
[X-Mat Studio](https://github.com/AaronGCProg/SamuraiShodown-XMatStudio) rồi
chạy `chargen` trên bảng animation của họ, **ngay trên máy bạn**.

> Kho đó **không có giấy phép nào**. Công khai trên GitHub không đồng nghĩa với
> mã nguồn mở: không có giấy phép thì mặc định là giữ toàn bộ bản quyền, và
> điều khoản GitHub chỉ cho phép xem và fork trong phạm vi GitHub, không cho
> phép phân phối lại. Vả lại sprite trong đó vẫn là của SNK, nên dù có giấy
> phép cũng không bao trùm được phần đó. Không có gì từ kho ấy được commit vào
> đây — `setup.sh` tải về máy bạn và chuyển đổi tại chỗ. Làm hơn thế thì phải
> xin phép tác giả.

Bản build sẵn cho Linux và Windows nằm ở
[Releases](https://github.com/thanhnhu/SamuraiOnline/releases), dành cho ai
không muốn cài bộ công cụ Go và FFmpeg. Chúng cũng không kèm nhân vật, vì cùng
lý do trên, nhưng có kèm `chargen` để bạn tự sinh. Gói Windows mang theo sẵn
DLL của FFmpeg và SDL nên không phải cài gì thêm — nhưng **cần card đồ hoạ chạy
được OpenGL 3.3**.

### Yêu cầu

Build thì cần Linux; trên Windows thì hoặc dùng bản phát hành sẵn, hoặc build
trong WSL / MSYS2.

```
git go pkg-config nasm yasm build-essential libxmp-dev libsdl2-dev
libgtk-3-dev libavformat-dev libavcodec-dev libavutil-dev
libswresample-dev libswscale-dev libavfilter-dev
```

**FFmpeg từ 7.1 trở lên.** Bản cũ hơn thiếu `AVBufferSrcParameters.color_space`
mà lớp video của Ikemen dùng, và build sẽ hỏng ngay ở đó. Debian trixie và
Ubuntu 25.04 đủ mới; Ubuntu 24.04 thì không.

`setup.sh` còn cần `curl` hoặc `wget` để tải cơ sở dữ liệu tay cầm của cộng
đồng. Thiếu nó game vẫn chạy, chỉ là tay cầm phải gán phím thủ công.

### Chơi online

```bash
# Trên một máy mà cả hai người chơi đều gọi tới được
cd SamuraiLobby
go run . -addr :8080 -relay-addr :8081 -relay-tcp-addr :8081 -relay-host <ip-public>
```

Trỏ `Netplay.LobbyURL` trong `Ikemen-GO/save/config.ini` về máy chủ đó, rồi
chọn **ONLINE LOBBY** ở menu chính. Trong danh sách phòng, **Enter** để vào
chơi và **S** để vào xem; người xem không chiếm ghế người chơi thứ hai, nên
phòng vẫn còn chỗ trong lúc có người theo dõi.

Các module: `Ikemen-GO/` (fork engine), `ggpo/` (fork thư viện rollback),
`SamuraiLobby/` (máy chủ), `SamuraiTools/chargen` và `SamuraiTools/sprtool`
(chuyển đổi asset). Không có `go.mod` ở gốc — mỗi thư mục là một module riêng.

### Kiểm tra đường mạng trước khi đổ lỗi cho game

`Ikemen-GO/cmd/netcheck` chạy đúng phần đục lỗ NAT và relay mà engine dùng,
đóng gói thành một file chạy độc lập không phụ thuộc thư viện nào, nên chép
sang máy nào cũng chạy. Nó cho biết hai bên có xuyên NAT được không, hay phải
đi vòng qua relay:

```bash
cd Ikemen-GO
CGO_ENABLED=0 go build -o netcheck ./cmd/netcheck
CGO_ENABLED=0 GOOS=windows GOARCH=amd64 go build -o netcheck.exe ./cmd/netcheck

./netcheck -lobby http://<may-chu-lobby>:8080 -role host  -room check
./netcheck -lobby http://<may-chu-lobby>:8080 -role guest -room check
```

Chạy lobby như một dịch vụ: xem `deploy/samurai-lobby.service` và
`deploy/samurai-lobby.default`. Đặt `LOBBY_RELAY_HOST` là địa chỉ mà **bên
ngoài** gọi tới được, không phải địa chỉ nội bộ của chính máy chủ; đặt sai thì
đục lỗ NAT sẽ hỏng một cách hoàn toàn im lặng.

---

## Hai hướng đi có thể chọn

Dự án khởi đầu bằng hướng **viết lại trong Unity**, rồi chuyển sang **fork một
engine có sẵn**. Cả hai đều được ghi lại ở đây để sau này muốn quay lại hướng
nào cũng được.

### A. Fork Ikemen GO — hướng đang theo

Ikemen GO là một engine đối kháng viết bằng Go, tương thích MUGEN, đã tích hợp
sẵn rollback netcode GGPO. Nhờ vậy công việc còn lại là: ghép trận, xuyên NAT
và chuyển đổi asset — chứ không phải vật lý, hộp va chạm hay netcode.

Tình trạng: máy chủ lobby và phần xuyên NAT chạy được và đã có test; đục lỗ NAT
đã được kiểm chứng giữa hai máy thật có NAT ở giữa; engine chạy; có một nhân
vật chơi được. Những chỗ còn thiếu được liệt kê thẳng thắn ở
[`ARCHITECTURE.vi.md` §8](ARCHITECTURE.vi.md#8-tình-trạng) — lớn nhất là chưa
từng thử qua internet, bản thân game chưa từng được chơi qua mạng, và chưa có
dàn nhân vật Samurai Shodown II.

### B. Viết lại bằng Unity — kế hoạch ban đầu, vẫn khả thi

Phần làm dở nằm ở `unity-prototype/`. Nó dùng **Photon (PUN)**, không phải
Mirror như phác thảo ban đầu.

Lộ trình ban đầu:

1. Phân tích mã đã dịch ngược: trạng thái game, xử lý input, va chạm
2. Chia thành các module: input, dựng hình, logic, mạng
3. Làm bản chơi offline trước
4. Thêm phần mạng, chọn giữa client-server hoặc ngang hàng, đồng bộ **input**
   chứ không đồng bộ trạng thái (chuẩn của thể loại đối kháng)
5. Tinh chỉnh độ trễ và chống lệch trạng thái; thêm giao diện, danh sách phòng,
   ghép trận

Cấu hình Unity như đã định:

- Unity 2022.3 LTS trở lên, mẫu **2D Core**
- Gói: Input System, TextMeshPro, 2D Animation, 2D PSD Importer
- Mạng: Photon PUN (đã import sẵn) hoặc
  [Mirror](https://github.com/vis2k/Mirror)

**Vì sao gác lại.** Làm lại một game đối kháng năm 1994 nghĩa là phải tái tạo
chính xác frame data, hộp va chạm, cửa sổ cancel và vật lý của nó, nếu không sẽ
không ra cảm giác của bản gốc. Thêm nữa, gắn rollback netcode vào một engine có
sẵn khó hơn nhiều so với việc thừa hưởng nó: rollback đòi hỏi mô phỏng tất định
và lưu/khôi phục toàn bộ trạng thái với chi phí thấp, những thứ phải được thiết
kế từ dòng code đầu tiên. Fork một engine đã giải xong cả hai bài toán đó sẽ
tới đích nhanh hơn rất nhiều.

**Muốn quay lại hướng này thì cần gì.** Phần việc đã làm chuyển sang dùng được
ngay:

- `SamuraiTools/sprtool` giải mã sprite `.SPR` của bản gốc — định dạng đã được
  dịch ngược trọn vẹn và ghi lại ở `ARCHITECTURE.vi.md` §5
- `SamuraiLobby` là dịch vụ HTTP + UDP/TCP thuần, không phụ thuộc engine, nên
  dùng nguyên vẹn làm backend ghép trận và relay cho Unity
- Thiết kế xuyên NAT (`Ikemen-GO/src/netpath/`) không phụ thuộc engine và đã là
  một package riêng không cần cgo; chỉ phần thao tác socket là đặc thù Go

Thứ vẫn còn thiếu chính là thứ đã chặn cả phần sprite: bản ghi `.SPR` không
chứa điểm neo, không nhóm animation, không thời lượng khung hình và không hộp
va chạm. Những dữ liệu đó nằm trong logic của game và phải dịch ngược tiếp —
dù bạn xây trên engine nào đi nữa.

---

## Pháp lý

Samurai Shodown II thuộc bản quyền © SNK. Dữ liệu game, sprite hay âm thanh
trích xuất từ nó, và file nhị phân đã dịch ngược **tuyệt đối không được** commit
hay phân phối. `.gitignore` và một bước kiểm tra trong CI đang cưỡng chế điều
này; đừng làm suy yếu cái nào trong hai.
