# Google Play Games 로그인·순위 설정

## 구현 범위

공식 Google Play Games Unity SDK 2.3.0을 Assets/GooglePlayGames에 추가했다. 기존 External Dependency Manager는 덮어쓰지 않았다. 원본: https://github.com/playgameservices/play-games-plugin-for-unity/releases/tag/v2.3.0

타이틀의 RANKING에서 Google Play Games 로그인과 게임별 순위 화면을 연다. Android 기기에서 설정이 완료되면 앱 시작 시 자동 로그인을 시도하며, 실패·취소 후 버튼으로 다시 로그인할 수 있다. 게스트도 게임을 플레이하고 로컬 기록을 저장할 수 있다. 로그인은 Google Play Games 프로필 인증이며 별도 서버 계정이나 클라우드 저장 동기화를 구현한 것은 아니다.

현재 프로젝트에는 실제 게임 서비스 프로젝트 ID와 리더보드 ID가 제공되지 않았다. 임의 ID는 넣지 않았으며 아래 설정 전에는 실제 로그인·순위 이용을 완료할 수 없다.

## Play Console

1. 기존 출시 앱에 연결된 Play Games Services 게임 프로젝트를 생성하거나 선택한다. 현재 Android 패키지 이름은 `com.HwangSeongMun.Blockdoku`다. 출시 앱의 패키지와 기존 서명 설정을 유지한다.
2. Android OAuth 인증 정보를 연결한다. 스토어 설치본 테스트에는 Play App Signing의 앱 서명 인증서 SHA-1을 사용한다. 직접 설치하는 개발 빌드는 해당 빌드 서명 인증서를 별도 등록해야 할 수 있다.
3. 아래 리더보드를 만든다. 지뢰찾기 시간은 초가 아닌 **밀리초 정수**로 전송한다.

| 용도 | 형식 | 정렬 |
| --- | --- | --- |
| Blockdoku | 숫자 점수 | 높은 점수 우선 |
| 10SUM | 숫자 점수 | 높은 점수 우선 |
| Minesweeper Beginner | 시간 | 짧은 시간 우선 |
| Minesweeper Intermediate | 시간 | 짧은 시간 우선 |
| Minesweeper Advanced | 시간 | 짧은 시간 우선 |

4. PGS 테스트 사용자와 앱 내부 테스트 사용자를 각각 등록한다. 필요한 게임 서비스 설정을 게시하고 내부 테스트 트랙에서 확인한다. 앱을 외부에 공개하는 단계는 별도로 진행한다.

## Unity

1. Android 빌드 대상으로 전환한다.
2. 공식 플러그인의 `Window > Google Play Games > Setup > Android setup`에서 Play Console의 Get resources로 받은 Android 리소스를 입력하고 설정한다. 이 과정에서 SDK 설정 자산과 Android manifest가 생성된다. 단순 로그인·리더보드에는 별도 서버 인증용 Web Client ID가 필요하지 않다.
3. `CasualParty > Leaderboard Settings` 메뉴를 열고 다섯 리더보드 ID를 입력한다. 실제 파일은 `Assets/Resources/LeaderboardSettings.asset`이다.
4. `Assets > External Dependency Manager > Android Resolver > Force Resolve`를 실행한다. SDK가 요구하는 games-v2 22.1.0과 nearby 18.5.0, 기존 AdMob 의존성을 함께 해결해야 한다. 기존 mainTemplate.gradle의 해석 결과는 Resolver로 갱신한다.
5. Android AAB를 빌드해 내부 테스트로 설치하고 인증·순위·광고를 검증한다.

## 기록 전송 정책

- 로그인한 상태에서 완료한 블록도쿠·10SUM 판의 점수, 승리한 지뢰찾기의 시간을 전송한다.
- 게스트 기록과 변경 전 로컬 최고 기록은 자동 업로드하지 않는다. 이전 Full Clear 버그가 반영된 점수를 신규 순위에 자동 유입시키지 않는다.
- 전송 실패 기록은 pendingScores.json에 계정별로 보관한다. 다음 로그인·앱 실행·순위 열기와 주기적인 재시도에서 다시 전송한다. 같은 계정·게임은 점수는 최대, 시간은 최소 기록을 보관한다.
- 자동 플레이 테스트 점수는 업로드하지 않는다.
- 순위 데이터는 클라이언트가 전송한다. 별도 서버의 점수 검증·부정행위 탐지 기능은 포함하지 않는다.

## 필수 실기기 확인

- 최초 실행 자동 인증, 수동 로그인, 취소 후 재시도, 네트워크 끊김, 앱 복귀.
- 다섯 순위 각각의 ID·정렬·시간 표시. 예: 12.345초 → 12345 전송.
- 점수는 더 높은 값, 시간은 더 짧은 값이 기존 기록을 갱신하는지 확인.
- 로그인한 A 계정의 전송 실패 기록이 B 계정에 업로드되지 않는지 확인.
- 리더보드를 닫은 후 입력 복구 및 AdMob과의 동시 사용.
- 콘솔의 리더보드 테스트 상태와 실제 서버 반영 결과를 기록한다. 에디터에서의 C# 컴파일 통과만으로 인증 성공을 판정하지 않는다.

공식 문서: [Unity 설정·인증](https://developer.android.com/games/pgs/unity/unity-start), [Unity 리더보드](https://developer.android.com/games/pgs/unity/leaderboards).
