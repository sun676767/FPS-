# Cover Combat

기존 FPS의 무기 그래픽·사격음·탄착 이펙트·명중 표시·탄약·재장전·무기 교체를 사용하는 엄폐 전투 모듈입니다. FPS, GameAssets, Framework, Input System이 필요합니다.

## 샘플 생성

1. `Tools > Cover Combat > Gameplay Flow Layout`을 엽니다.
2. `Create Sample Scene (additive)`로 두 구역을 생성합니다. 생성된 씬을 저장하고 다른 씬을 닫은 뒤 Play합니다.
3. WASD로 Trigger에 진입하면 이동이 잠깁니다. 우클릭 유지 시 엄폐 그래픽이 숨겨지고 줌인하며 피격 가능한 노출 상태가 됩니다. 이때 좌클릭으로 사격합니다. 우클릭을 놓으면 보호됩니다.
4. 1/2/3은 무기 교체, R은 재장전입니다. 적을 모두 처치하면 게이트가 열리고 다음 구역으로 이동할 수 있습니다. 마지막 구역은 Complete, 사망은 Failed가 됩니다. 재시작은 Play를 다시 실행합니다.
5. Escape는 커서를 해제하고 좌클릭은 다시 잠급니다.

## 기존 FPS 연결

기존 씬은 Flow Player와 Camera를 지정하고 Layout의 `Connect FPS Weapons to Flow Player`를 실행합니다. 같은 카메라의 기존 FpsLectureGame이 있으면 재사용하며 없으면 생성합니다. 기존 이동·체력 스크립트는 중복 실행하지 마세요. FpsLectureGame은 활성 상태로 유지합니다.

CoverCombatPlayer는 이동·시점·엄폐·체력을 담당합니다. 발사는 FpsLectureGame이 담당하고 IFpsCombatDriver를 통해 노출 중 발사 허용과 적 피격을 연결합니다. 무기 수치는 FPSWeapons.json, 업그레이드는 기존 FPS 저장값을 사용합니다. 구역 진행은 현재 Play 세션에만 유지됩니다.

## 모델과 두 가지 애니메이션 패턴

`Runtime/Model`의 Swat 모델과 FBX 애니메이션을 사용합니다. 원본 클립은 변경하지 않으며 Controller와 복제 클립은 `CoverCombat/Animations`에 생성됩니다.

- **CoverShootIdle**: Taking Cover Idle ↔ Shooting Pistol을 blendSeconds로 블렌딩합니다. idleSeconds 동안 엄폐하고 attackSeconds 동안 공격합니다. SidePeek이면 Hidden/Exposed Point 사이로 Actor도 이동합니다.
- **Gunplay**: Taking Cover Idle → Gunplay 전체 재생 → Taking Cover Idle을 반복합니다. 클립 자체의 앉기·일어서기·사격을 사용하며 SidePeek 위치 이동은 적용하지 않습니다. attackSeconds로 클립을 자르지 않고 끝까지 재생합니다. 실제 발사는 정규화된 진행도 gunplayFireStart~gunplayFireEnd 사이에서만 실행합니다(기본 35~70%). 실제 사격 프레임을 보며 이 값을 조정하세요.
- **피격·사망**: 실제 총탄 충돌 시 Health가 감소하고 onHit을 호출합니다. 0이면 발사를 중단하고 Collider를 끈 뒤 Death From The Back을 재생합니다. Death는 반복하지 않고 마지막 자세를 유지하며 공격 상태로 복귀하지 않습니다. 구역 처치에도 즉시 반영됩니다.

기존 씬에서는 적 또는 적들이 포함된 구역을 선택하고 `Tools > Cover Combat > Models`에서 `Apply Cover - Shoot Idle to Selected Enemies` 또는 `Apply Idle - Gunplay to Selected Enemies`를 실행합니다. 기존 Actor와 피격 Collider를 Swat 모델 및 뼈에 부착한 Collider로 교체합니다. 모델은 유효한 Humanoid Avatar가 필요합니다.

피격 Collider는 머리·가슴·골반·팔·다리를 따라 움직입니다. Gunplay의 앉은 자세에 고정된 서 있는 캡슐이 남지 않습니다. 앞의 엄폐 Collider가 먼저 맞으면 데미지가 전달되지 않습니다. Gunplay 엄폐물은 앉으면 숨고 일어서면 상체가 드러나는 높이로 배치하세요. 새 샘플은 각 구역에 고정 CoverShootIdle, 측면 CoverShootIdle, Gunplay 적을 한 명씩 생성합니다.

## 다른 월드에서 구성

- Flow의 areas 순서대로만 진입할 수 있습니다. Area마다 BoxCollider Trigger, Player Anchor, Player Cover, Enemies를 연결합니다. 생성기는 Trigger에 kinematic Rigidbody를 부착합니다.
- Player Cover의 visuals에는 숨길 Renderer만 넣습니다. Collider는 유지됩니다. 조준 Ray가 엄폐물 위나 옆으로 통과하도록 Anchor를 배치하세요. 샘플 카메라 높이는 1.65m, 플레이어 엄폐물은 1.4m입니다.
- SidePeek의 Hidden/Exposed Point는 움직이는 Actor의 자식이 아닌 고정 기준점이며 발 위치를 나타냅니다. Assigned Cover에는 실제 Collider가 있는 엄폐물을 연결합니다. Muzzle은 모델 손을 따라갑니다.
- hitMask에 월드·엄폐물·피격 레이어를 포함하세요. 플레이어 커스텀 데미지는 CoverCombatPlayer.TakeDamage를 사용해야 엄폐 보호가 적용됩니다. FpsPlayerHealth와 별개입니다.
- Animator의 Root Motion을 사용합니다. 생성된 클립의 회전·Y·XZ 이동은 Bake Into Pose를 해제하여 추출합니다. Animator가 있는 적은 코드로 SidePeek 위치를 보간하지 않으며 클립의 이동을 따릅니다. Hidden Point는 초기 배치 기준이고 Exposed Point 도착 여부로 사격을 제한하지 않습니다. Animator가 없는 적만 기존 위치 보간을 사용합니다. Controller의 상태 이름을 바꾸면 Enemy 필드도 수정하세요.

기존 씬은 적 또는 구역을 선택하고 `Tools > Cover Combat > Models > Enable Root Motion on Selected Enemies`를 실행하면 모델을 교체하지 않고 적용됩니다. 런타임에도 Root Motion을 활성화합니다. 외부 커스텀 클립은 원본을 수정하지 않으므로 Import Settings에서 Bake Into Pose를 직접 확인하세요. 클립에 측면 이동이 없으면 SidePeek에서도 측면 이동은 발생하지 않습니다. 반복 클립의 누적 이동과 엄폐물 간격은 Play에서 확인하세요. Root Motion은 Collider에 의한 이동 차단을 자동 제공하지 않습니다.
- onShot/onHit/onDeath, Area의 onEntered/onCleared, Flow의 onComplete/onFailed로 효과·퀘스트·다음 씬을 연결합니다. exitGates는 클리어 시 비활성화됩니다.

## 검증

Layout의 Validate Setup 후 Play에서 두 패턴의 반복, Gunplay 발사 구간, 자세에 따른 피격, 엄폐물 선충돌, 체력 0의 사망 애니메이션, 구역 클리어를 확인하세요. 실제 FBX 자세·총구 위치·엄폐물 높이·레이어 배치는 Unity에서 확인해야 합니다.
