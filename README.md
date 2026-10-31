# 뜰채 바다 (fish-pond-unity)

빔프로젝터로 바닥에 **둥근 산호초 바다**를 비추고, 4명이 뜰채로 바다 생물을 떠서 자기 낚시통에 옮기는 경쟁 게임입니다.
화면에 보이는 모든 것(바다, 낚시통, 타이머, 글자, 보정 마커)은 둥근 원 안에 들어갑니다. 원 밖은 완전한 검정입니다.

- **엔진**: Unity 6 (6000.3 LTS 기준, 6000.0 이상이면 열림) · Built-in 렌더 파이프라인
- **그래픽**: 에셋 없이 코드로 생성합니다. 바로 실행되고, Asset Store 모델로 생물별 교체도 됩니다.
- **입력**: 키보드·마우스·멀티터치로 테스트하고, 실제 설치에서는 IR 카메라 추적 좌표(UDP)를 받습니다.

---

## 1. 바로 실행하기

1. 저장소를 받습니다.
   ```bash
   git clone https://github.com/kepler8350/fish-pond-unity.git
   ```
2. **Unity Hub → Add → Add project from disk**로 `fish-pond-unity` 폴더를 고릅니다.
   설치된 Unity 버전이 다르다는 안내가 나오면 가지고 있는 Unity 6 버전을 선택하세요.
3. 처음 열면 `Assets/FishPond/Scenes/FishPond.unity` 씬이 자동으로 만들어지고 열립니다.
   안 열리면 상단 메뉴 **Fish Pond → 게임 씬 열기**를 누르세요.
4. ▶ 재생을 누르고 **Space**로 시작합니다.

> 처음 열고 나면 Unity가 만든 `ProjectSettings/`, `Packages/packages-lock.json`, 씬 파일을 커밋해 두세요.

## 2. 조작

| 플레이어 | 키보드 | 낚시통 위치 |
|---|---|---|
| P1 (빨강) | W A S D | 왼쪽 아래 |
| P2 (노랑) | ↑ ← ↓ → | 오른쪽 아래 |
| P3 (초록) | I J K L | 오른쪽 위 |
| P4 (파랑) | T F G H | 왼쪽 위 |

마우스·터치로는 가까운 뜰채를 잡아 끌 수 있습니다. 멀티터치를 쓰면 여럿이 동시에 움직일 수 있습니다.

| 키 | 기능 |
|---|---|
| Space / Enter | 시작 · 다시 하기 |
| Esc | 일시정지 / 계속 |
| R | 처음 화면 |
| F1 | 운영 설정 패널 |
| F2 | 추적 보정 마커 (둥근 화면 안 4곳) |
| F11 | 전체화면 |

## 3. 규칙과 생물

- 뜰채를 생물 위에 **0.4초** 멈추면 잡힙니다. 뜰채 둘레의 흰 링이 차오르는 게 신호입니다.
- 뜰채 하나에 **최대 2마리**까지 담깁니다.
- 담은 채로 **너무 빨리 휘두르면** 놓칩니다.
- 빠르게 들이밀면 근처 물고기가 도망갑니다.
- 자기 색 낚시통 위로 옮기면 점수가 올라갑니다. 동점이면 마릿수가 많은 쪽이 이깁니다.

| 생물 | 점수 | 특징 |
|---|---|---|
| 흰동가리 | 1 | 가장 흔함 |
| 노랑탱 | 1 | 흔하고 조금 빠름 |
| 나비고기 | 2 | 눈에 검은 띠 |
| 블루탱 | 2 | 빠름 |
| 엔젤피시 | 3 | 파랑·노랑 줄무늬 |
| 복어 | 3 | 뜰채가 가까이 오면 **부풀어서** 잡는 시간 2배 |
| 쏠배감펭 | 4 | 느리지만 드묾, 부채 같은 지느러미 |
| 오징어 | 4 | 물을 뿜어 툭툭 튀어나가고, 빠른 뜰채를 보면 반대로 분사해 도망 |
| 문어 | 5 | 기어다니다 뜰채가 오면 **먹물**을 쏘고 도망 (6초 쿨타임) |
| 가오리 | 5 | 날개를 너울거리며 부드럽게 선회, 잡는 시간 1.2배 |
| 바다거북 | 7 | 가장 드묾, 느리고 겁이 없음, 잡는 시간 1.4배 |
| 해파리 | – | 잡을 수 없음. **닿으면 찌릿**: 담은 생물을 놓치고 뜰채 1.6초 마비 |

수치는 `Assets/FishPond/Scripts/SpeciesCatalog.cs`의 기본값이나, 아래 4장의 생물 설정 에셋에서 바꿀 수 있습니다.

## 4. Asset Store 무료 에셋으로 생물 바꾸기

### 확인된 무료 에셋 (2026년 10월 기준)

| 에셋 | 내용 | 파이프라인 | 비고 |
|---|---|---|---|
| [Fish - PolyPack](https://assetstore.unity.com/packages/3d/characters/animals/fish/fish-polypack-202232) | 로우폴리 물고기 | Built-in · URP · HDRP | 2021년 v1.0 |
| [Pixit3D - Fish Free Pack](https://assetstore.unity.com/packages/package/397878) | 물고기 | Built-in · URP · HDRP | 2026-09 출시, Unity 6000.5로 제작 |
| [Fish Alive - FREE Samples](https://assetstore.unity.com/packages/package/362806) | 리깅·애니메이션 물고기 샘플 | Built-in · URP | HDRP 미지원 |
| [Low Poly Fish](https://assetstore.unity.com/packages/package/339618) | 로우폴리 물고기 | Built-in · URP · HDRP | 2025-10 |
| [Quirky Series - FREE Animals Pack](https://assetstore.unity.com/packages/package/178235) | 귀여운 동물 모음 | Built-in · URP | 문어·오징어 검색에 걸리지만 포함 여부는 페이지에 명시 안 됨 |

**문어·오징어·해파리는 확인된 무료 에셋이 없었습니다.** 그래서 이 프로젝트는 세 생물을 코드로 직접 그립니다(촉수 애니메이션 포함). 유료로는 [Friendly Octopus](https://assetstore.unity.com/packages/3d/characters/animals/friendly-octopus-295330)($4.99) 같은 선택지가 있습니다.

이 프로젝트는 Built-in 파이프라인이라 **URP 전용 에셋은 분홍색으로 보입니다.** 위 표에서 Built-in 호환인 것을 고르세요.

### 가져오기 (import)

1. 위 링크에서 **Add to My Assets**를 누릅니다 (Unity 계정 로그인 필요).
2. Unity에서 **Window → Package Manager**를 열고, 왼쪽에서 **My Assets**를 고릅니다.
3. 에셋을 선택하고 **Download → Import**를 누른 뒤, 가져오기 창에서 **Import**를 누릅니다.
4. 가져온 폴더를 Project 창에서 `Assets/ThirdParty/` 아래로 **드래그해 옮깁니다.**
   이 폴더는 `.gitignore`에 들어 있어 GitHub에 올라가지 않습니다. Asset Store 라이선스상 재배포가 금지되어 있기 때문입니다. 다른 PC에서는 같은 방법으로 다시 가져오면 됩니다.

### 생물에 연결

1. 메뉴 **Fish Pond → 생물 설정 에셋 만들기 (에셋 교체용)**를 누릅니다.
   `Assets/FishPond/Resources/FishPond/SpeciesCatalog.asset`이 만들어지고 선택됩니다.
2. Inspector에서 바꿀 생물(예: 흰동가리)을 펼치고, 가져온 물고기 프리팹을 **Prefab Override**에 끌어다 놓습니다.
3. ▶ 재생해서 방향을 확인합니다. 머리가 진행 방향을 보지 않으면 **Prefab Rotation**을 조정하세요.
   기준은 "모델 앞(+Z) = 진행 방향, 모델 위(+Y) = 카메라 쪽"입니다. 흔한 보정값은 Y를 90, -90, 180으로 바꾸는 것입니다.
4. 크기는 몸길이에 맞춰 자동 조정됩니다. 더 키우거나 줄이려면 **Prefab Scale**을 바꿉니다.

점수, 등장 확률, 속도, 잡는 시간도 같은 화면에서 바꿀 수 있습니다.

## 5. 설치 (빔프로젝터 + 카메라)

### 운영 설정 (F1)
- 게임 시간, 생물 수, 해파리 수
- 배치: 원 반지름의 66%까지가 생물이 헤엄치는 바다, 그 바깥 고리가 산호초입니다. 낚시통은 대각선 4곳, 타이머는 좌우에 있습니다.
- **투사 보정**: 크기, 가로 비율(원이 타원으로 보일 때), 좌우·상하 이동
- **뜰채 손잡이 그리기**: 실물 뜰채를 쓸 때는 끄세요.
- **저장**을 누르면 다음 실행에도 유지됩니다.

### 카메라 추적
천장 카메라가 뜰채 중앙의 850nm IR LED를 찾아, LED 개수(1~4개)로 플레이어를 구분합니다.

- 권장 카메라: Waveshare OV9281 1MP Mono USB Camera (A). 120° 화각, 글로벌 셔터, IR 필터 없음. 렌즈 앞에 850nm 패스 필터를 붙입니다.
- 뜰채 LED 배치: P1 1개, P2 2개(일자), P3 3개(삼각), P4 4개(사각). 점 사이 간격은 6cm 이상.

```bash
cd tools/tracker
pip install -r requirements.txt

# 1) 보정: Unity에서 F2로 둥근 화면 안 마커를 띄우고, 뜰채를 마커 1→2→3→4 위에 차례로 올리며 스페이스
#    게임 화면이 16:9가 아니면 --aspect 1.6 처럼 비율을 넣으세요
python ir_tracker.py --calibrate

# 2) 실행 (Unity가 같은 PC에서 돌고 있어야 함)
python ir_tracker.py --show
```

F1 패널의 "카메라 추적" 줄에서 P1~P4 옆이 ●로 바뀌면 좌표가 들어오고 있는 것입니다.
점이 잘 안 잡히면 `--threshold`(밝기 기준)와 `--exposure`(노출)를 조정하세요.

UDP 메시지 형식은 한 줄에 `플레이어번호 가로 세로`입니다. 좌표는 투사 화면 기준 0~1이고 원점은 왼쪽 위입니다(예: `1 0.523 0.331`). 다른 추적 장치를 쓸 때도 이 형식으로 7777 포트에 보내면 됩니다.

## 6. 빌드
**File → Build Profiles**(또는 Build Settings)에서 Windows를 고르고 Build를 누릅니다. 게임 씬은 처음 만들 때 빌드 목록에 자동으로 추가됩니다.
설치 PC에서는 프로젝터를 보조 모니터로 두고 F11로 전체화면을 켭니다.

## 7. 폴더 구조

```
Assets/FishPond/
  Scripts/
    FishPondGame.cs         게임 본체 (배치, 규칙, 상태, 그리기)
    FishPondGame.Panel.cs   F1 운영 패널, F2 보정 마커
    Creature.cs             생물 행동 (복어 부풀기, 오징어 분사, 문어 먹물, 해파리)
    CreatureVisuals.cs      생물 그래픽 (코드 생성 / 프리팹 교체)
    Art.cs                  스프라이트·텍스처 생성
    NetAndBucket.cs         뜰채, 낚시통
    Fx.cs                   물결, 점수 팝업, 먹물, 효과음 합성, 바닥 글자
    InputBridge.cs          옛/새 입력 시스템 자동 대응
    TrackingReceiver.cs     UDP 추적 좌표 수신
    SpeciesCatalog.cs       생물 설정
    PondSettings.cs         운영 설정 저장
  Editor/FishPondMenu.cs    상단 "Fish Pond" 메뉴
  Resources/FishPond/       물·선 셰이더 (빌드에 자동 포함)
tools/tracker/              IR 카메라 추적기 (Python + OpenCV)
```
