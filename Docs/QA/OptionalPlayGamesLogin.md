# 선택 로그인 동작과 정책 검토

변경일: 2026-11-06.

## 정책·법률 질문에 대한 구분

Google Play Games Services v2의 자동 플랫폼 인증은 공식 문서에서 설명하는 정상적인 통합 방식이다. 자동이라는 사실만으로 정책 위반 또는 불법이라고 판단할 수 없다. 개인정보 처리는 실제 처리 항목, 목적, 고지, 적법한 처리 근거, 서비스 지역 등에 따라 별도로 판단해야 한다. 이 코드 변경은 앱의 선택 UX를 제공하는 것으로, 개인정보 처리에 필요한 모든 법적 동의를 대신하지 않는다.

현재 앱은 별도 자체 계정을 생성하지 않는다. 앱 코드에서 순위 기능을 위해 플레이어 ID와 닉네임을 가져오고, 계정별 점수·시간을 전송한다. SDK 자체의 데이터 처리와 AdMob 데이터 처리는 별도로 개인정보처리방침·데이터 보안 공개에 반영할 사항을 확인해야 한다.

## 구현한 흐름

1. 앱을 실행하면 기존 로컬 데이터와 설정을 사용할 수 있다. 앱 시작 코드에서는 Authenticate/ManuallyAuthenticate를 호출하지 않는다.
2. 처음 타이틀에 들어오면 Google Play Games 로그인 또는 로컬 플레이를 선택하는 창을 표시한다. 로그인 시 프로필을 사용한 순위 조회·점수 전송이 가능하다고 설명한다.
3. 로그인 버튼을 선택했을 때만 ManuallyAuthenticate를 호출한다. 성공 후 ID·닉네임을 가져오고 보관된 해당 계정의 기록을 재전송한다.
4. 로컬 플레이 또는 뒤로 가기를 선택하면 인증하지 않는다. 게임을 플레이해도 새 순위 점수를 대기열에 넣거나 전송하지 않는다.
5. 최초 선택창을 처리했는지만 playGamesChoice.json에 저장한다. 로그인 허가를 저장하거나 다음 실행에 자동 로그인하지 않는다.
6. 이후 RANKING에서 로그인 버튼을 선택할 수 있다. 로그인 전 순위표 버튼은 비활성화된다. 로그인 실패·취소 후 같은 버튼으로 재시도할 수 있다.
7. 로그인한 세션에서는 기존처럼 게임 종료 시 순위 기록을 전송한다. 앱을 종료하고 다시 실행하면 앱의 순위 연동은 다시 비활성 상태에서 시작한다.
8. 이미 전송한 서버 기록과 과거 계정별 대기 기록은 이 변경으로 삭제하지 않는다. 게스트 상태에서는 과거 대기 기록도 전송하지 않는다.

## SDK 자체 인증의 한계

PGS v2에는 PlayGamesInitProvider 등 네이티브 초기화와 플랫폼 인증 흐름이 있다. 기존 Android 빌드의 병합 manifest에서도 이 Provider를 확인했다. 따라서 C#의 시작 인증 호출을 없앤 것만으로 Google SDK 자체의 백그라운드 인증이나 기존 사용자의 환영 표시까지 차단했다고 주장하지 않는다.

앱이 소유한 별도 Android 라이브러리 manifest에 SUPPRESS_GAME_PROFILE_CREATION=true를 추가했다. 공식적으로 지원되는 이 설정은 프로필이 없는 사용자의 자동 프로필 생성 안내를 억제한다. **자동 플랫폼 인증 전체를 끄는 설정은 아니다.** 플러그인이 생성하는 manifest는 직접 덮어쓰지 않았다.

SDK 차원까지 포함한 인증·네트워크 활동의 완전한 지연은 이번 구현에서 보장하지 않는다. 앱은 사용자 선택 전 플랫폼 프로필을 조회하거나 순위 기록을 전송하지 않도록 제한한다.

## 실기기 확인

- 기존 프로필이 있는 기기에서 최초 선택창과 Google 환영 표시의 관계 확인.
- 프로필이 없는 기기에서 자동 프로필 생성 안내가 억제되는지 확인.
- 로컬 플레이 후 pendingScores.json에 새 기록이 추가되지 않는지 확인.
- RANKING에서 로그인 선택 후 기존 계정 기록 전송과 순위 조회 확인.
- 앱 완전 종료·재실행 후 닉네임 조회와 순위 전송이 사용자 로그인 선택 전에는 실행되지 않는지 확인.
- 새 빌드의 최종 병합 AndroidManifest.xml에 SUPPRESS_GAME_PROFILE_CREATION=true가 포함됐는지 확인.

## 공식 자료

- [PGS 플랫폼 인증](https://developer.android.com/games/pgs/platform-authentication)
- [Unity의 자동 프로필 생성 안내 억제](https://developer.android.com/games/pgs/unity/unity-start#prevent-auto-triggered-profile-creation)
- [Google Play 사용자 데이터 정책](https://support.google.com/googleplay/android-developer/answer/10144311?hl=ko)
- [개인정보 보호법 제15조](https://www.law.go.kr/LSW/lsLinkCommonInfo.do?chrClsCd=010202&lsJoLnkSeq=1029334803)

## ?? ?? ??

12? ??, ?? 0?. ??? ?? ? ? ????? ??? ??? ?? ??? ?? ???? ?? ??? ?? ??? ????. ?? ?? ?? ??? ?? ???? ????. [?? ??](OptionalLoginRegressionResults.xml). Android? C# ??? ? ?? ??? ?? ?? ??. ???? SDK ??? ?? Android manifest ??? ? ?? ???? ?? ??? ????.
