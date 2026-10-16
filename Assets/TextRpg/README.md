# 칸별 Text RPG 이벤트 템플릿

## 바로 사용할 예제

`Examples/TextRpgPlayer.prefab`을 씬에 드래그하면 예제 맵과 플레이어 초기값이 연결된 `RpgEventController`가 생깁니다. UI를 자동 생성하지 않습니다.

- `Example Event Map`: A1~O10 전체 150칸에 기본 이벤트 지정. B2와 C3는 개별 이벤트로 덮어씁니다.
- `Quiet Clearing`: 약초를 조사하거나 휴식. 조사 시 지능 판정으로 약초 획득/피해가 갈립니다.
- `Injured Traveler` (B2): 기본 선택은 상태 살피기/길 안내. 붕대·약초를 전용 슬롯에서 사용하면 별도의 치료·해독 사건이 열립니다.
- `Sealed Cache` (C3): 기본 선택은 힘으로 개봉/떠나기. 전용 슬롯에서 열쇠를 사용하면 이중 바닥을 발견합니다. 보급품만 챙기거나 함정을 무릅쓰고 숨은 물자를 노릴 수 있습니다.
- 초기 플레이어: 체력 80/100, 각 스탯 5, 붕대 2개, 열쇠 1개.

## 새 이벤트 작성

Project 창 우클릭 → **Create → Text RPG → Event**. 기본 선택지 2개가 생성됩니다. 에셋을 복제해서 사건을 계속 추가할 수 있습니다.

| 필드 | 의미 |
| --- | --- |
| Title / Narrative | 사건 제목과 본문 |
| Repeatable | 같은 칸에서 해결 후 다시 발생할지 여부. 기본값은 한 번만 발생 |
| Choices | 최소 2개인 기본 선택지. 아이템 조건/소모 없음 |
| Item Interactions | 아이템 사용 전용 슬롯에서 열리는 특수 사건 목록 |
| Item Interactions → Item / Amount / Consume | 사용할 아이템·필요 수량·소모 여부. Consume을 끄면 열쇠 같은 도구는 유지 |
| Item Interactions → Title / Narrative / Choices | 특수 사건 제목·본문·선택지(최소 2개). 기본 Choices와 별도 |
| Use Stat Check | 켜면 Check Stat이 Minimum Stat 이상일 때 Success, 미만이면 Failure |
| Success / Failure → Narrative | 행동 후 보여줄 결과 문장 |
| Health Change | 양수 회복, 음수 피해. 체력은 0~최대 체력으로 제한 |
| Max Health Change | 최대 체력 증가/감소. 최소 최대 체력은 1 |
| Stats | 힘·민첩·지능·행운의 증감. 각 스탯 최소 0 |
| Rewards | 결과로 가방에 추가할 아이템과 수량 |

아이템은 **Create → Text RPG → Item**으로 생성합니다. 같은 아이템 에셋을 특수 상호작용과 보상에 연결하세요. 이름이 같아도 서로 다른 에셋이면 다른 아이템입니다. Icon이 있으면 가방/전용 슬롯에 표시되고, 없어도 이름·수량으로 구분할 수 있습니다.

가방에서 아이템을 골라 슬롯에 넣는 것만으로는 소모되지 않습니다. 사용 버튼을 확정하면 해당 사건의 Item Interactions에서 같은 아이템을 찾아 수량을 검사합니다. 사용할 수 없거나 수량이 부족하면 상태 변화 없이 거절합니다. 사용 가능하면 Consume 설정에 따라 그때 수량을 차감하고 **별도의 특수 사건 선택지로 전환**합니다.

특수 사건에 들어간 뒤에는 기본 선택으로 돌아가거나 아이템을 다시 사용할 수 없습니다. 특수 선택을 마쳐야 이동할 수 있습니다. 실패해도 이미 사용한 소모품은 반환하지 않습니다. 특수 결과는 보상이 더 좋거나 오히려 피해가 커지도록 자유롭게 작성합니다. 한 사건에 같은 아이템의 특수 상호작용을 중복 지정할 수 없습니다.

스탯 판정은 확률 주사위가 아닌 **기준치 비교**입니다. 판정이 꺼져 있으면 항상 Success를 적용합니다. 결과 적용 순서는 아이템 비용 → 최대 체력/체력 → 스탯 → 보상입니다.

## 모든 칸에 이벤트 연결

**Create → Text RPG → Event Map**에서 Default Event를 지정하면 모든 칸에 이벤트가 생깁니다. Overrides에는 별도 사건을 넣을 칸만 추가합니다. Column은 A=0 ~ O=14, Row는 1~10입니다.

같은 이벤트 에셋을 여러 칸이 공유해도 해결 여부는 **칸별**로 관리합니다. A1을 해결해도 A2의 이벤트는 남아 있습니다. 중복 좌표, 비어 있는 칸, 선택지 2개 미만, 잘못된 아이템 조건·보상은 실행 전에 거절합니다.

## 씬 UI 및 이동 로직 연결

`RpgEventController` 인스펙터의 Event Map과 Player를 설정합니다. 플레이어 상태는 공유 에셋이 아닌 해당 컨트롤러의 세션 데이터입니다.

```csharp
// A1 진입. 그리기 모드의 클릭과 구분해서 이동 로직에서 호출합니다.
controller.EnterCell(0, 1);

// 사건 본문과 선택지. 생성할 UI는 씬에 직접 배치하고 참조를 연결하세요.
var encounter = controller.CurrentEvent;
var body = controller.Narrative;
bool enabled = controller.Session.CanChoose(0, out string reason);

// 첫 번째 선택. UI Button의 OnClick에서 Choose(int)에 0을 지정해도 됩니다.
controller.Choose(0);
```

- `On Event Opened`: 기본 사건/아이템 특수 사건을 표시합니다. `Session.CurrentTitle`, `Narrative`, `Session.CurrentChoices`를 사용해야 현재 단계에 맞는 내용이 나옵니다.
- `On Event Resolved`: `Narrative`와 `Player`의 체력·스탯·가방을 갱신합니다. 해결 후 `CurrentEvent`는 null입니다.
- `On Rejected`: `LastError`를 표시합니다. 선택 불가 사유는 `CanChoose`로 미리 확인할 수도 있습니다.
- `EnterCurrentCell()`은 인스펙터의 Column/Row에 설정한 칸을 엽니다.
- `UseItem(item)`은 아이템 특수 사건 진입용입니다. `Session.CanUseItem(item, out reason)`으로 사용 가능 여부를 확인할 수 있습니다.
- 최대 선택지 수는 제한하지 않습니다. 표시할 선택지 수에 맞는 UI를 씬에 배치하세요.

진행 중인 사건을 해결하기 전에는 다른 칸 진입을 거절합니다. 체력 0이면 탐험과 선택을 거절합니다. UI 이벤트 콜백을 통해 게임 오버 화면 등을 연결할 수 있습니다.

현재 플레이어 위치·상태·해결 이력은 메모리에 존재합니다. 진행 저장·불러오기와 확률 판정은 포함하지 않습니다. 기존 지도 그림 저장 파일은 변경하지 않습니다.

## Map 씬에서 이동하기

`Assets/Scenes/Map.unity`에는 플레이어 컨트롤러, 마커, 주변 칸 표시와 이벤트 패널을 연결했습니다. 모든 UI는 씬에 저장되어 있으며 런타임에 생성하지 않습니다.

1. Play를 시작하면 A1의 이벤트가 열립니다. 선택 후 **계속 탐험**을 누릅니다.
2. **Move** 모드에서 녹색으로 표시된 주변 8칸을 클릭하면 한 칸 이동합니다. 클릭을 끌어서 놓는 드래그는 이동으로 처리하지 않습니다.
3. 이동한 칸의 이벤트를 해결하기 전에는 다음 칸으로 이동할 수 없습니다. 이미 해결한 칸은 이벤트·보상 없이 다시 지나갈 수 있습니다.
4. 이동하면 플레이어를 화면 가운데로 맞춥니다. **Player center** 버튼은 현재 위치 추적을 다시 켭니다. 지도 끝에서도 플레이어를 가운데에 두므로 지도 밖 여백이 보일 수 있습니다.
5. 마우스 가운데 버튼으로 지도를 끌면 추적이 해제됩니다. **Fit whole map**은 지도 전체를 보여줍니다. Draw/Erase와 색상 선택은 그리기 모드로 전환하며 이동시키지 않습니다.

인스펙터 설정:

- `Text RPG Player → RpgEventController`: 시작 Column/Row, Allow Diagonal(대각선 허용), Open Starting Event.
- `Map UI → Map Viewport → MapPainter`: Movement Mode, Follow Player, Player Zoom(전체 지도 맞춤 배율 대비 배수).
- `Map Board → Player Marker / Neighbour Highlight 1~8`: 마커 모양·색상과 주변 칸 강조 색상.
- `Map Viewport → Event Panel`: 사건 본문과 선택 버튼 3개. 선택지가 4개 이상인 이벤트는 **다음 선택지**로 페이지를 전환합니다.
- `Map UI → RPG UI Bindings`: 컨트롤러와 씬 UI 참조. 컨트롤러의 On Event Opened / Resolved / Rejected는 이 컴포넌트의 표시 메서드에 연결되어 있습니다.

## 가방과 아이템 슬롯 사용

가방 8칸에는 보유 아이템의 이름·수량·선택적 아이콘이 표시됩니다. 아이템 종류가 8개보다 많으면 가방 페이지 버튼으로 넘깁니다. 보상/소모 후 즉시 갱신합니다.

1. 사건이 열린 상태에서 오른쪽 가방의 아이템을 클릭합니다.
2. 사건 패널 아래 **아이템 사용 전용 슬롯**에 선택한 아이템이 표시됩니다. 선택 해제는 수량에 영향을 주지 않습니다.
3. 사용 가능할 때 **아이템으로 특수 상호작용**을 누릅니다. 소모 여부·필요 수량과 위험 안내를 미리 확인할 수 있습니다.
4. 특수 사건 본문과 새로운 선택지에서 행동을 결정합니다. 결과 적용 후 가방과 체력·스탯이 갱신됩니다.

가방 슬롯·아이템 사용 슬롯·사용/선택 해제 버튼도 모두 Map 씬 오브젝트입니다. 코드로 생성하지 않습니다. `RPG UI Bindings → Bag and dedicated item slot`에서 각 참조를 바꿀 수 있습니다.

이동 로직에서 `controller.MoveToCell(column, row)`를 호출하면 인접 칸·이벤트·체력 조건을 검사합니다. `EnterCell`은 초기 진입/직접 지정용이므로 일반 이동에는 `MoveToCell`을 사용합니다.

## 검증

`dotnet run --project "Assets/TextRpg/Tests~/DomainChecks.csproj"`로 런타임 게임 규칙 32개를 검증합니다. 이동 규칙 외에도 기본 선택의 무소모, 아이템 사용 가능 여부 확인의 무소모, 부족/미지원 아이템 거절, 특수 사건 전환·실패·추가 보상·중복 사용 차단을 포함합니다. 테스트는 실제 Runtime 소스를 사용하며 Unity 기반 타입/인스펙터 속성만 대체합니다. Unity의 에셋 역직렬화와 화면 동작은 별도 에디터 확인 대상입니다. `Tests~` 폴더는 Unity가 임포트하지 않습니다.
