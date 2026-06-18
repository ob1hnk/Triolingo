<p align="center">
  <img src="./assets/logo.png" width="200"/>
</p>

<h1 align="center">GoGo Golem!</h1>

<p align="center">
  <b>음성 · 손동작 · 편지</b>로 골렘과 교감하며 정착지를 일구는 멀티모달 3D 스토리 어드벤처
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Unity-6000.0.62f1-000000?logo=unity&logoColor=white" alt="Unity"/>
  <img src="https://img.shields.io/badge/Python-3.11-3776AB?logo=python&logoColor=white" alt="Python"/>
  <img src="https://img.shields.io/badge/FastAPI-009688?logo=fastapi&logoColor=white" alt="FastAPI"/>
  <img src="https://img.shields.io/badge/Firebase-Firestore-FFCA28?logo=firebase&logoColor=black" alt="Firebase"/>
  <img src="https://img.shields.io/badge/MediaPipe-00A98F?logo=google&logoColor=white" alt="MediaPipe"/>
</p>

<p align="center">
  <b>장르</b> 3D 판타지 성장 어드벤처 &nbsp;·&nbsp; <b>플랫폼</b> Windows
</p>


## 스토리

**가문의 전통을 깨부수는, 작고 어리숙한 '걸작'을 만드세요!**

<p align="center">
  <img src="./assets/GoGoGolem_Home.gif" alt="게임 주요 장면 1" width="800"/>
</p>

대대로 마을을 수호하는 **완벽한 골렘**을 만들어야 하는 장인 가문의 후계자, 그리고 그 손에서 태어난 **예상 밖의 골렘**이 함께 성장하는 판타지 어드벤처입니다.

제작 중 실수로 탄생한 이 **작고 호기심 많은 친구**는 주인공과 **영혼이 연결**되어 버립니다. 사람과 골렘의 영혼 연결은 마을의 금기. 당신은 이 모자란 듯 특별한 골렘과 함께 여정을 떠나 **음성, 손짓, 편지**로 교감하며, 낯선 정착지의 주민들을 돕고 진정한 수호자로 거듭납니다.

**과연 당신의 '실수'는 마을의 영웅이 될 수 있을까요?**


## 게임 정보

<p align="center">
  <img src="./assets/GoGoGolem Forest.png" width="680"/>
</p>
<p align="center">
  <img src="./assets/GoGoGolem 골렘 대화.png" width="680"/>
</p>
<p align="center">
  <img src="./assets/GoGoGolem 제스처인식.png" width="680"/>
</p>
<p align="center">
  <img src="./assets/GoGoGolem Room.png" width="680"/>
</p>

---

### 당신의 목소리와 몸짓으로 완성되는, 새로운 형태의 어드벤처입니다.

마이크와 웹캠을 통해, 골렘과 직접 대화하고 교감하세요.
말을 걸고, 손을 뻗고, 신호를 보내며 — 컨트롤러 너머의 행동이 그대로 게임 속 세계에 이어집니다.

**당신만의 방식으로 골렘과 호흡을 맞추세요.**

같은 상황에서도, 어떻게 말하고 어떻게 움직이느냐에 따라 골렘의 반응과 행동은 달라집니다. 정답은 없습니다. 당신과 골렘이 만들어가는 방식이 곧 플레이 스타일이 됩니다.

**몸짓으로 완성되는 협동 마법을 익히세요.**

스킬을 발동시키기 위해 손동작을 하세요. 골렘은 당신의 움직임을 따라하며 힘을 끌어냅니다. 처음엔 서툴지만, 점점 더 자연스럽고 강력한 호흡으로 이어집니다.

**편지를 통해, 또 다른 이야기를 이어가세요.**

하루가 끝나면 부모님께 편지를 씁니다. 오늘 겪은 일, 골렘과의 순간들, 그리고 아직 풀리지 않은 질문들. 다음 날 도착하는 답장은 당신이 보지 못했던 시선과 새로운 힌트를 전해줍니다.

---

## 기술 스택

| Layer       | Stack |
|-------------|-------|
| **Client**  | Unity 6000.0.62f1 · C# · New Input System · Animation Rigging · Yarn Spinner · MediaPipe · WebCamTexture |
| **Server**  | Python 3.11 · FastAPI · WebSocket · LiteLLM (ModelRouter 기반 Fallback) |
| **AI**      | OpenAI Realtime API · LLM 편지 응답 생성 |
| **DB**      | Firebase Firestore (편지) · Unity PlayerPrefs / JSON (로컬 세이브) |
| **Infra**   | Docker · docker-compose |

### 아키텍처 특징

* **Unity**
  `Managers` 싱글톤과 **ScriptableObject 기반 이벤트 버스**를 활용해 시스템 간 결합도를 낮췄습니다.
  또한 **MVP(Presenter–View) 패턴**을 적용해 UI 로직을 명확히 분리했습니다.

* **AI 서버**
  **Clean Architecture + DDD**를 기반으로 Server / Application / Domain / Infra의 4계층 구조로 설계했습니다.
  Port 인터페이스를 통해 도메인 레이어가 특정 구현 기술에 의존하지 않도록 구성했습니다.

* **AIBridge**
  HTTP(`AIHttpClient`)와 WebSocket(`RealtimeWebSocketClient`)을 분리하여
  통신 방식에 따라 독립적인 클라이언트 구조를 설계했습니다.

## 실행 환경

| 요구사항 | 내용 |
|---------|------|
| OS      | Windows |
| 필수 장치 | 마이크 · 웹캠 |

## 데모 다운로드 및 실행

직접 빌드하지 않고도 미리 빌드된 데모 버전을 내려받아 바로 실행할 수 있습니다. **Windows PC** 에서만 동작하며, 마이크와 웹캠이 연결되어 있어야 합니다.

**1) 다운로드 페이지 접속.**
[고고골렘 Linktree](https://linktr.ee/triolingogogolem) 에 접속해 첫 번째 링크인 **'고고골렘! - 데모 다운로드'** 를 클릭합니다. Google Drive 공유 폴더로 연결됩니다.

**2) 데모 파일 내려받기.**
Drive 폴더에서 `GoGoGolem!.zip` 파일 오른쪽 끝의 세로 점 세 개(추가 작업) 메뉴를 클릭하고 **다운로드** 를 선택합니다. 파일 용량이 커 바이러스 검사를 할 수 없다는 안내가 나오면 **다운로드** 를 한 번 더 클릭하면 내려받기가 시작됩니다.

**3) 압축 해제 및 실행.**
다운로드 폴더에 받은 `GoGoGolem!.zip` 의 압축을 풉니다. 압축이 풀린 폴더로 들어가 `GoGoGolem.exe` 를 더블 클릭해 실행합니다. Windows 보안 알림창이 나타나면 **허용** 을 눌러 게임을 시작합니다.

> 사진과 함께 정리된 자세한 안내는 같은 Drive 폴더의 `고고골렘! 데모 다운 및 실행 방법.docx` 문서에서 확인할 수 있습니다.

## 프로젝트 구조

```
.
├── GoGoGolem/src/
│   ├── ai/                              # AI 서버 (FastAPI + WebSocket)
│   │   ├── interaction/
│   │   │   ├── core/                    # 공통 인프라 (LiteLLM Router, Firebase)
│   │   │   ├── server/                  # FastAPI Router / WebSocket / DTO
│   │   │   ├── speech/                  # 음성 도메인 (Realtime / VAD)
│   │   │   └── text/                    # 편지 도메인 (LLM 응답 생성)
│   │   ├── Dockerfile
│   │   ├── docker-compose.yaml
│   │   └── pyproject.toml
│   │
│   └── unity/GoGoGolem/
│       ├── Assets/
│       │   ├── Scripts/                 # C# (Managers, Quest, Dialogue, AIBridge, …)
│       │   ├── Scenes/                  # 시작 · 프롤로그 · 숲 · 제스처 · 정착지 · 숙소
│       │   ├── Prefabs/
│       │   ├── MediaPipeUnity/          # MediaPipe Unity Plugin
│       │   ├── Fonts/ · Sprites/ · Settings/
│       │
│       └── ProjectSettings/
│
├── docs/
├── assets/
└── README.md
```


---
## How to Install

설치를 진행하기 전에 아래 항목들을 먼저 준비합니다.

| 항목                      | 설명                                                                 |
| ----------------------- | ------------------------------------------------------------------ |
| Windows 10/11 PC        | Unity 클라이언트를 빌드하고 실행할 PC입니다. 마이크와 웹캠이 연결되어 있어야 합니다.                |
| Unity 6000.0.62f1       | Unity Hub를 설치한 뒤 Hub에서 해당 버전을 설치합니다.                               |
| Python 3.13 이상          | AI 서버 실행에 필요합니다. 서버를 AWS에서만 구동할 경우 로컬 PC에는 설치하지 않아도 됩니다.           |
| uv                      | Python 의존성과 가상환경을 관리하는 도구입니다. `pip install uv` 로 설치할 수 있습니다.       |
| Docker & Docker Compose | AI 서버를 컨테이너 환경에서 실행할 때 사용합니다. 로컬과 서버 환경 모두 설치를 권장합니다.              |
| OpenAI API Key          | 음성 대화 및 편지 생성 기능에 사용됩니다. OpenAI 플랫폼에서 발급받을 수 있습니다.                 |
| Firebase 프로젝트           | 편지 데이터를 저장할 Firestore 데이터베이스입니다. 설정 방법은 아래 데이터 및 데이터베이스 절에서 설명합니다. |
| AWS 계정                  | AI 서버를 클라우드에 배포할 경우에만 필요합니다.                                       |

### 1. 저장소 클론

먼저 프로젝트 저장소를 내려받습니다. Git이 설치되어 있지 않다면 먼저 Git을 설치한 뒤, 터미널(Windows의 경우 PowerShell 또는 Git Bash)에서 아래 명령을 실행합니다.

```bash
git clone https://github.com/ob1hnk/GoGoGolem.git
cd GoGoGolem
```

클론이 완료되면 `GoGoGolem/src/ai`(AI 서버)와 `GoGoGolem/src/unity/GoGoGolem`(Unity 클라이언트) 디렉터리가 생성된 것을 확인할 수 있습니다.

### 2. AI 서버를 AWS EC2에 처음부터 구축하기

이 절은 AWS 사용 경험이 없는 사람을 기준으로 작성했습니다. 새 EC2 인스턴스를 생성하고 AI 서버를 배포하기까지의 과정을 순서대로 설명합니다. 로컬 환경에서만 서버를 실행할 계획이라면 이 절은 건너뛰고 **3. 로컬에서 AI 서버 실행**으로 이동해도 됩니다.

**(1) AWS 계정 생성 및 콘솔 로그인**

먼저 AWS 홈페이지에서 계정을 생성합니다. 가입 과정에서 신용카드 정보와 휴대폰 인증이 필요합니다.

가입이 완료되면 AWS Management Console에 로그인하고, 화면 우측 상단의 Region 메뉴에서 사용할 리전을 선택합니다. 국내에서 사용할 경우 **Asia Pacific (Seoul) ap-northeast-2**를 선택하면 됩니다. 이후 생성하는 리소스는 모두 해당 리전에 생성됩니다.

**(2) EC2 인스턴스 생성**

콘솔 상단 검색창에서 `EC2`를 검색한 뒤 EC2 대시보드로 이동합니다. **Launch Instance** 버튼을 눌러 인스턴스를 생성하고 다음과 같이 설정합니다.

* **Name**: `gogo-golem-ai-server` 등 식별하기 쉬운 이름
* **OS Image (AMI)**: Ubuntu Server 22.04 LTS (64-bit x86)
* **Instance Type**: 간단한 테스트는 `t2.micro`로 가능하지만, 보다 안정적인 동작을 위해 `t3.small` 이상을 권장
* **Key Pair**: 새 키 페어를 생성한 뒤 `.pem` 파일 다운로드

다운로드한 `.pem` 파일은 이후 SSH 접속에 사용되므로 별도로 보관합니다.

**(3) 보안 그룹 설정**

Network Settings에서 새로운 Security Group을 생성하고 다음 인바운드 규칙을 추가합니다.

* **SSH (TCP 22)**: 서버 접속용 포트. 가능하면 Source를 **My IP**로 제한
* **Custom TCP (TCP 8000)**: AI 서버 포트. Unity 클라이언트 접속을 위해 `0.0.0.0/0` 허용

설정을 마친 뒤 **Launch Instance**를 클릭하면 인스턴스가 생성됩니다.

상태가 **Running**으로 변경되면 인스턴스 상세 화면에서 **Public IPv4 Address**를 확인할 수 있습니다. 이 주소가 이후 Unity 클라이언트가 접속할 서버 주소가 됩니다.

**(4) SSH 접속**

`.pem` 파일이 있는 위치에서 터미널을 열고 아래 명령을 실행합니다.

```bash
# 키 파일 권한 설정 (macOS/Linux 또는 Git Bash)
chmod 400 gogo-golem-key.pem

# 서버 접속
ssh -i gogo-golem-key.pem ubuntu@13.124.xx.xx
```

처음 접속할 때 호스트 신뢰 여부를 묻는 메시지가 나타나면 `yes`를 입력합니다. 접속에 성공하면 프롬프트가 `ubuntu@ip-...` 형태로 변경됩니다.

**(5) Docker 설치**

접속한 서버에서 아래 명령을 실행합니다.

```bash
sudo apt-get update
sudo apt-get install -y ca-certificates curl git

curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh

sudo usermod -aG docker ubuntu
```

권한 변경을 적용하기 위해 한 번 로그아웃한 뒤 다시 SSH로 접속합니다.

이후 아래 명령이 정상적으로 동작하면 설치가 완료된 것입니다.

```bash
docker --version
docker compose version
```

**(6) 소스 코드 배포 및 환경 변수 설정**

서버에서 저장소를 클론한 뒤 AI 서버 디렉터리로 이동합니다.

```bash
git clone https://github.com/ob1hnk/GoGoGolem.git
cd GoGoGolem/GoGoGolem/src/ai
```

이후 `.env` 파일을 생성하고 필요한 환경 변수를 입력합니다.

```env
OPENAI_API_KEY=sk-...
AWS_ACCESS_KEY_ID=...
AWS_SECRET_ACCESS_KEY=...
AWS_REGION_NAME=us-east-1
FIREBASE_CREDENTIALS_PATH=./gogo-golem-firebase-adminsdk-fbsvc-xxxxxx.json
```

Firebase 콘솔의 **프로젝트 설정 → 서비스 계정 → 새 비공개 키 생성** 메뉴에서 Admin SDK 키를 발급받고, 해당 JSON 파일을 `GoGoGolem/src/ai/` 디렉터리에 업로드합니다.

파일 업로드에는 `scp`를 사용합니다.

```bash
scp -i gogo-golem-key.pem gogo-golem-firebase-adminsdk-fbsvc-xxxxxx.json \
ubuntu@13.124.xx.xx:~/GoGoGolem/GoGoGolem/src/ai/
```

**(7) 컨테이너 실행 및 동작 확인**

Docker Compose를 이용해 이미지를 빌드하고 서버를 실행합니다.

```bash
docker compose up --build -d
docker compose logs -f
curl http://localhost:8000/health
```

로컬 PC 브라우저에서 아래 주소에 접속해 FastAPI 문서 페이지가 열리는지 확인합니다.

```text
http://13.124.xx.xx:8000/docs
```

문서 페이지가 정상적으로 표시되면 서버 배포가 완료된 것입니다.

Unity 클라이언트에서는 다음 주소를 사용해 서버에 연결할 수 있습니다.

* TLS 미사용: `ws://13.124.xx.xx:8000`
* TLS 사용: `wss://13.124.xx.xx:8000`

### 3. MediaPipe Unity Plugin

손동작(Hand Landmarker)과 포즈(Pose Landmarker) 인식에는 [MediaPipe Unity Plugin](https://github.com/homuler/MediaPipeUnityPlugin)을 사용합니다.

**(1) 플러그인 본체는 별도 설치가 필요 없습니다.**

플러그인 소스는 이미 저장소(`Assets/MediaPipeUnity/`)에 포함되어 있으며, MIT 라이선스 파일도 함께 들어 있습니다. 따라서 패키지를 따로 임포트하거나 설치할 필요 없이, Unity Hub에서 **6000.0.62f1** 버전으로 `GoGoGolem/src/unity/GoGoGolem` 프로젝트를 열면 그대로 사용할 수 있습니다.

**(2) 모델 파일(`.bytes`)도 이미 저장소에 포함되어 있어 추가 다운로드가 필요 없습니다.**

추론에 사용하는 모델 파일은 플러그인 패키지의 `Packages/com.github.homuler.mediapipe/PackageResources/MediaPipe/` 경로에 함께 포함되어 있습니다. `hand_landmarker.bytes`, `pose_landmarker_lite.bytes`, `pose_landmarker_full.bytes`, `pose_landmarker_heavy.bytes` 등이 모두 저장소에 들어 있으므로, 별도로 모델을 내려받을 필요가 없습니다.

> 사용할 모델은 `Assets/Scripts/Runtime/Multimodal/Gesture/Config/GestureConfig.cs`에서 결정됩니다. 기본값은 Hand `hand_landmarker.bytes`, Pose `pose_landmarker_lite.bytes` 두 개이며, Pose 모델은 `GestureConfig`의 `PoseModel` 값(Lite / Full / Heavy)으로 바꿀 수 있습니다.

**(3) 에디터에서는 추가 설정 없이 바로 동작합니다.**

모델 로딩 방식은 `Assets/MediaPipeUnity/Samples/Scenes/AppSettings.asset`의 **Asset Loader Type** 으로 결정되며, 에디터에서 사용하는 `Local` 방식은 위 `PackageResources/MediaPipe/` 폴더에서 모델을 직접 읽습니다. 모델이 이미 그 위치에 포함되어 있으므로, 에디터에서 제스처 씬을 플레이하는 데에는 어떤 추가 작업도 필요하지 않습니다.

> **빌드(`.exe`) 시에는 이야기가 다릅니다.** `Local` 방식은 에디터에서만 동작하고, `PackageResources` 폴더도 빌드 결과물에는 포함되지 않습니다. 빌드된 게임에서 손동작 인식을 사용하려면 모델을 `Assets/StreamingAssets/` 폴더로 복사하고 Asset Loader Type을 `StreamingAssets`로 두어야 합니다. 이 절차는 빌드 단계에서만 필요하므로 [How to Build](#unity-클라이언트-빌드) 절에서 설명합니다.

### 4. Unity 클라이언트에 AI 서버 주소 연결

이제 Unity 클라이언트가 접속할 AI 서버 주소를 설정합니다.

1. Unity Editor에서 `Assets/Scripts/Runtime/Multimodal/Config/` 경로의 `AIBridgeConfig` ScriptableObject를 선택합니다.
2. **Server URL** 필드에 사용할 서버 주소를 입력합니다.

   * 로컬 환경에서 실행할 경우: `ws://localhost:8000`
   * AWS 등 원격 서버를 사용할 경우: `ws://<서버 Public IP>:8000`
   * TLS를 적용한 경우: `wss://<도메인>:8000`

---

## How to Build

빌드는 크게 **AI 서버를 실행하는 과정** 과 **Unity 클라이언트를 실행 파일로 만드는 과정** 두 단계로 나뉩니다.

### AI 서버 기동

AI 서버는 별도의 컴파일 과정이 없는 Python 애플리케이션입니다. 따라서 여기서 "빌드"는 필요한 의존성을 설치한 뒤 서버 프로세스를 실행하는 것을 의미합니다. 실행 방법은 다음 두 가지가 있습니다.

**방법 A — 직접 실행 (개발용).** 코드를 수정하면서 바로 결과를 확인해야 할 때 사용합니다. `--reload` 옵션이 적용되어 있어 소스 코드가 변경되면 서버가 자동으로 재시작됩니다.

```bash
cd GoGoGolem/src/ai

make run
# 위 명령은 아래와 동일합니다.
uv run uvicorn interaction.server.app:app --host 0.0.0.0 --port 8000 --reload
```

**방법 B — Docker 컨테이너 실행 (운영·시연용).** 실행 환경을 고정하고 동일한 환경에서 재현 가능하게 실행하고 싶을 때 사용합니다. 아래 명령은 [AWS 배포](#2-ai-서버를-aws-ec2-에-처음부터-구축하기) 절에서 사용한 것과 동일합니다.

```bash
cd GoGoGolem/src/ai

docker compose up --build -d   # 이미지 빌드 후 백그라운드 실행
docker compose logs -f         # 실시간 로그 확인
curl http://localhost:8000/health   # 헬스 체크 ({"status":"ok"} 류 응답)
```

어떤 방법을 사용하든 서버가 정상적으로 실행되면 브라우저에서 `http://localhost:8000/docs`(원격 서버라면 `http://<서버 IP>:8000/docs`)에 접속할 수 있습니다. FastAPI가 자동 생성한 REST API 문서를 통해 WebSocket 및 HTTP 엔드포인트가 정상적으로 등록되었는지 확인할 수 있습니다.

### Unity 클라이언트 빌드

Unity 클라이언트는 Windows 실행 파일(`.exe`) 형태로 빌드합니다. Unity Hub에서 **6000.0.62f1** 버전으로 `GoGoGolem/src/unity/GoGoGolem` 프로젝트를 엽니다. 처음 프로젝트를 열 경우 패키지 복원과 셰이더 컴파일 때문에 몇 분 정도 소요될 수 있습니다.

대부분의 빌드 설정(빌드 씬 목록, 해상도, 정의 심볼 등)은 이미 프로젝트에 커밋되어 있으므로, 아래 절차는 새로 설정한다기보다 **값이 올바른지 확인하는 과정** 에 가깝습니다. 다만 `StreamingAssets` 폴더는 `.gitignore` 대상이라 저장소에 포함되지 않으므로, 이 부분만은 직접 채워 넣어야 합니다.

#### 1. StreamingAssets 폴더 채우기 (직접 추가 필요)

`Assets/StreamingAssets/` 폴더는 `.gitignore`로 제외되어 있어 클론 직후에는 비어 있거나 존재하지 않습니다. 빌드 전에 아래 파일들을 이 폴더에 직접 넣어 줍니다.

* **MediaPipe 모델 파일** — `Packages/com.github.homuler.mediapipe/PackageResources/MediaPipe/` 에 있는 `hand_landmarker.bytes` 와 `pose_landmarker_lite.bytes` (사용하는 Pose 모델이 다르면 해당 파일) 를 `Assets/StreamingAssets/` 폴더에 **하위 폴더 없이** 복사합니다. `GestureConfig`가 파일명만으로 모델을 참조하므로 경로가 아닌 파일명이 일치해야 합니다.
* **`google-services-desktop.json`** — Firestore(편지 데이터) 연동에 사용하는 데스크톱용 Firebase 설정 파일입니다. `Assets/google-services.json` 을 배치하면 Firebase Unity SDK가 이 파일을 `Assets/StreamingAssets/google-services-desktop.json` 으로 자동 생성합니다. (`google-services.json` 역시 API 키가 포함되어 있어 `.gitignore` 대상이므로 직접 배치해야 합니다. [Firebase Unity SDK 설치 가이드](#firebase-unity-sdk-설치-가이드) 참고)

> `Local` 로더(에디터 전용)와 달리 빌드된 `.exe`는 `StreamingAssets` 폴더만 읽을 수 있습니다. 위 파일들이 빠지면 빌드된 게임에서 손동작 인식 또는 편지 저장 기능이 동작하지 않습니다.

#### 2. Build Profiles 및 설정값 확인

**File → Build Profiles** 를 열고 다음 항목들이 올바르게 설정되어 있는지 확인합니다.

* **Scene List** — `Home`, `Intro`, `Forest`, `Gesture Detection Wind`, `Room` 씬 옆에 체크 표시가 되어 있는지 확인합니다.
* **Platform Settings → Development Build** — 일반 배포 빌드에서는 **꺼 둡니다.** 체크하면 빌드에서 인게임 디버그 콘솔을 통해 로그 확인 및 명령어 사용이 가능하므로, 디버깅이 필요할 때만 켭니다.

이어서 **Player Settings** 에서 다음 값들을 확인합니다.

* **Resolution and Presentation** — Fullscreen Mode가 **Windowed**, Default Screen Width / Height가 **1920 × 1080** 인지 확인합니다.
* **Other Settings → Configuration → Allow downloads over HTTP** — **Always allowed** 로 설정되어 있는지 확인합니다.
* **Other Settings → Script Compilation → Scripting Define Symbols** — `USE_PUBLIC_SERVER` 가 추가되어 있는지 확인합니다.

#### 3. 빌드 및 실행

**File → Build Profiles → Build** 를 클릭한 뒤 빌드 폴더를 지정하면 빌드가 시작됩니다. 빌드가 완료되면 지정한 폴더에 `.exe` 파일과 데이터 폴더(`GoGoGolem_Data/`)가 생성됩니다.

게임을 실행하려면 해당 폴더의 `GoGoGolem.exe` 파일을 더블 클릭합니다.

---
## How to Test

테스트는 크게 **코드 정적 검사**, **음성·Realtime 기능 통합 테스트**, **클라이언트–서버 연동 수동 점검** 세 단계로 진행합니다.

### 1. 코드 정적 검사 (Lint)

AI 서버 코드의 문법 오류와 스타일 일관성을 검사합니다. 코드를 수정한 뒤 가장 먼저 수행하는 단계입니다.

```bash
cd GoGoGolem/src/ai

make lint   # ruff 로 interaction/ 전체 정적 검사
make fix    # 자동 수정 가능한 항목 수정 + 포매팅
```

`make lint` 가 오류 없이 종료되면 코드 스타일 기준을 통과한 상태입니다.

### 2. 음성 / Realtime 통합 테스트

서버를 실행한 상태에서 실제 마이크 입력과 WebSocket 스트리밍이 정상적으로 동작하는지 확인합니다. 아래 테스트들은 OpenAI API를 직접 호출하므로 `.env` 파일에 `OPENAI_API_KEY` 가 설정되어 있어야 하며, 마이크도 연결되어 있어야 합니다.

```bash
cd GoGoGolem/src/ai

# 음성 스트리밍 WebSocket 기본 흐름 테스트
uv run python interaction/server/tests/test_speech_v1.py

# OpenAI Realtime API 연동 테스트 (실시간 음성 대화)
uv run python interaction/server/tests/test_realtime_v1.py

# 마이크 입력 기반 Realtime 대화 테스트
uv run python interaction/server/tests/test_mic_realtime.py
```

각 스크립트는 콘솔에 STT 결과와 LLM 응답을 출력합니다. 사용자의 발화가 텍스트로 정확히 변환되고, 골렘의 응답이 자연스럽게 생성된다면 음성 파이프라인이 정상적으로 동작하는 것입니다.

---

## evaluation (실험 데이터 / 실험 결과)

게임의 세 가지 핵심 상호작용 시스템인 **편지(Letter)**, **음성(Speech)**, **모션 인식(Gesture)** 에 대해 정량 평가를 진행했습니다. 평가 스크립트와 실측 데이터, 결과 그래프는 모두 `GoGoGolem/evaluation/` 디렉터리에 포함되어 있습니다.

### 1. 편지 시스템 — Hint disguise 평가

부모 NPC가 보내는 답장 편지에는 **다음 퀘스트에 대한 힌트** 가 포함되어 있습니다. 다만 이를 게임 지시문처럼 직접 알려주기보다, **부모의 추억담 속에 자연스럽게 녹여 전달하는 것(Hint disguise)** 을 목표로 설계했습니다.

이를 평가하기 위해 LLM-as-judge 방식의 1~5점 루브릭을 사용했습니다. 실제 운영 프롬프트로 편지를 생성한 뒤, 동일 모델을 채점자로 활용해 5개 도메인 케이스를 평가한 결과 평균 **4.0 / 5점** 을 기록했습니다.

```bash
cd GoGoGolem/src/ai
.venv/bin/python ../../evaluation/letter_system/hint_disguise_eval.py
```

* 코드·루브릭 상세: [`evaluation/letter_system/`](GoGoGolem/evaluation/letter_system/)
* 결과물: `hint_disguise_results.json` (편지 전문, 점수, 평가 근거 포함), `hint_disguise_scores.png` (케이스별 점수 그래프)

### 2. 음성 대화 시스템 — 응답 지연 평가

사용자가 말을 마친 시점부터 AI 응답이 시작될 때까지의 **end-to-end 응답 지연 시간** 을 측정해 초기 구조와 개선 구조를 비교했습니다.

평가를 위해 음성 usecase의 STT·LLM 단계에 **OpenTelemetry span** 을 삽입하고, **Grafana Tempo** 와 OTLP HTTP 기반 수집 환경을 통해 trace 데이터를 수집했습니다. 또한 `ContextVar` 를 사용해 trace ID를 전파함으로써 하나의 WebSocket 세션 전체가 동일한 trace로 연결되도록 구성했습니다.

비교 대상은 다음 두 가지 모드입니다.

| 모드               | 구조               | 평균 응답 시간     | 구성                          |
| ---------------- | ---------------- | ------------ | --------------------------- |
| **Pipeline**     | STT → LLM 직렬 처리  | **5,544 ms** | STT 2,287 ms + LLM 3,257 ms |
| **Realtime API** | 입력·생성 스트리밍 병렬 처리 | **2,294 ms** | 동시 처리                       |

초기 파이프라인 구조에서는 Whisper STT가 끝난 뒤에야 LLM 호출이 시작되는 직렬 병목이 존재했습니다. 이후 **OpenAI Realtime API** 기반 구조로 변경하면서 음성 입력과 응답 생성을 병렬 스트리밍 방식으로 처리할 수 있게 되었고, 평균 응답 시간이 **5,544 ms에서 2,294 ms로 감소해 약 2.4배 개선** 되었습니다.

측정에 사용한 실측 trace 데이터(5회 × 2모드)인 `sample_traces.json` 과 분석 스크립트도 함께 제공되며, 아래 명령으로 결과 그래프를 다시 생성할 수 있습니다.

```bash
python GoGoGolem/evaluation/speech/analyze_traces.py
```

* 코드·방법론 상세: [`evaluation/speech/`](GoGoGolem/evaluation/speech/)
* 결과물: `response_time.png` (평균 응답 시간 bar chart 및 샘플별 지연 scatter plot)

### 3. 모션 인식 시스템 — 떨림 보정 필터 평가

웹캠 기반 손동작 인식에서는 프레임마다 손 좌표가 미세하게 흔들리는 **떨림(jitter)** 이 발생합니다. 이를 줄이기 위해 스무딩 필터를 적용할 수 있지만, 필터 강도가 높아질수록 동작 반응이 늦어지는 **지연(lag)** 이 발생하는 문제가 있습니다.

이 시스템에서는 세 가지 필터(**None / Moving Average / One Euro**)를 동일한 raw 녹화 데이터에 대해 **오프라인으로 재적용** 하여 비교했습니다. CSV 파일에는 활성화된 필터와 관계없이 원본 좌표(raw)가 저장되므로, 한 번의 녹화만으로도 세 필터를 동일한 입력 조건에서 재현 가능하게 평가할 수 있습니다.

평가에는 다음 세 가지 지표를 사용했습니다.

| 지표         | 의미                               | 방향          |
| ---------- | -------------------------------- | ----------- |
| **jitter** | 손을 정지한 상태에서의 프레임 간 3D RMS 떨림     | 낮을수록 좋음     |
| **lag**    | 손을 좌우로 움직일 때 raw 대비 필터 출력 지연(ms) | 낮을수록 좋음     |
| **fps**    | 런타임 프레임레이트(성능 비용)                | 차이가 작을수록 좋음 |

분석 결과, **One Euro 필터** 가 떨림 감소와 지연 증가 사이의 트레이드오프 측면에서 가장 균형 잡힌 성능을 보였습니다. 또한 `optimize_oneeuro.py` 를 이용한 파라미터 스윕을 통해 **minCutoff = 2, beta = 2** 가 트레이드오프 곡선의 무릎(knee)에 해당한다는 것을 확인했습니다. 이는 지연을 더 줄이려고 할 경우 떨림이 급격히 증가하기 직전의 지점으로, 최종 설정값으로 채택했습니다.

평가에 사용한 One Euro 필터 구현은 Unity 런타임의 `GolemLandmarkAnimator.cs` 와 동일한 수식을 Python으로 포팅한 버전입니다. 따라서 측정 결과가 실제 게임에서의 동작 특성을 그대로 반영하도록 구성했습니다.

```bash
cd GoGoGolem/evaluation

# 필터 3종 비교 (정지=jitter / 왕복=lag / fps)
python analyze_gestures.py

# One Euro 파라미터 스윕 + 트레이드오프 곡선
python optimize_oneeuro.py
```

> 두 스크립트는 게임 내 제스처 로거가 생성한 `gesture_*.csv` 녹화 파일을 입력으로 사용합니다. 정지 상태 녹화(jitter 측정용)와 좌우 왕복 동작 녹화(lag 측정용)가 각각 최소 1개 이상 필요합니다. 실행 결과로 `summary.csv`, `plot_bars.png`, `plot_motion_raw_vs_filt.png`, `plot_still_jitter.png`, `plot_tradeoff.png` 가 생성됩니다.

* 코드·방법론 상세: [`evaluation/analyze_gestures.py`](GoGoGolem/evaluation/analyze_gestures.py), [`evaluation/optimize_oneeuro.py`](GoGoGolem/evaluation/optimize_oneeuro.py)

---
## 데이터 및 데이터베이스

### Firebase Firestore (편지 데이터)

편지 시스템에서 사용하는 데이터(플레이어가 작성한 편지와 부모 NPC의 답장)는 **Firebase Firestore** 클라우드 데이터베이스에 저장됩니다.

* **컬렉션**: `letters` — 플레이어가 작성한 편지와 이에 대한 NPC 답장이 문서 단위로 저장됩니다.
* **서버 측 접근**: AI 서버는 Firebase Admin SDK 서비스 계정 키(`gogo-golem-firebase-adminsdk-fbsvc-*.json`)를 사용해 Firestore에 접근합니다. 이 키는 Firebase 콘솔의 **프로젝트 설정 → 서비스 계정 → 새 비공개 키 생성** 메뉴에서 발급할 수 있으며, `GoGoGolem/src/ai/` 경로에 배치한 뒤 `.env` 파일의 `FIREBASE_CREDENTIALS_PATH` 에서 해당 경로를 지정합니다.
* **클라이언트 측 연결**: Unity 클라이언트는 `Assets/google-services.json` 에 포함된 프로젝트 설정을 이용해 Firestore에 연결합니다.

### Firebase Unity SDK 설치 가이드

Unity 클라이언트에서 Firestore를 사용하려면 Firebase Unity SDK를 별도로 임포트해야 합니다. SDK 용량이 매우 크기 때문에(약 1.2GB) 저장소에는 포함하지 않았으며, 아래 절차에 따라 설치합니다.

**1) Firebase Unity SDK 다운로드.**
Firebase 공식 다운로드 페이지에서 `firebase_unity_sdk_13.7.0.zip` 파일을 내려받아 압축을 해제합니다. 용량이 큰 편이므로 안정적인 네트워크 환경에서 다운로드하는 것을 권장합니다.

**2) Unity 패키지 임포트.**
Unity Editor에서 프로젝트를 연 뒤 **Assets → Import Package → Custom Package** 를 선택하고, 압축을 해제한 폴더에서 아래 두 패키지만 임포트합니다.

* `FirebaseFirestore.unitypackage` (편지 데이터 저장 및 조회)
* `FirebaseAnalytics.unitypackage`

**3) google-services.json 확인.**
Firebase 콘솔에서 발급받은 `google-services.json` 파일을 `Assets/google-services.json` 경로에 추가합니다.

**4) 초기화 확인.**
Unity Editor의 Console 창에 Firebase 초기화 관련 오류가 출력되지 않으면 설정이 정상적으로 완료된 것입니다.

> 참고: Firebase Unity SDK는 Android/iOS 환경에서 가장 완전한 기능을 제공하지만, 본 프로젝트에서는 Firestore 데이터 연동 목적으로만 사용합니다. 따라서 별도의 모바일 빌드 타겟 전환 없이도 Editor 및 Windows 환경에서 편지 데이터를 저장하고 조회할 수 있습니다.

### 로컬 세이브 데이터

퀘스트 진행 상태, 인벤토리, 숙소(방) 상태처럼 클라우드 동기화가 필요하지 않은 게임 데이터는 **Unity PlayerPrefs** 와 **JSON 파일** 형태로 클라이언트 로컬에 저장됩니다.


---

## 사용 오픈소스

| 라이브러리 / 패키지 | 용도 | 라이선스 |
|---|---|---|
| [MediaPipe Unity Plugin](https://github.com/homuler/MediaPipeUnityPlugin) | Unity 내 손동작(Hand Landmarker) · 포즈(Pose Landmarker) 실시간 인식 | MIT |
| [FastAPI](https://fastapi.tiangolo.com/) | AI 서버 WebSocket / REST API 프레임워크 | MIT |
| [LiteLLM](https://github.com/BerriAI/litellm) | OpenAI / AWS Bedrock 모델 라우팅 및 Fallback | MIT |
| [Firebase Admin Python SDK](https://firebase.google.com/docs/admin/setup) | 서버 측 Firestore 접근 | Apache 2.0 |
| [Firebase Unity SDK](https://firebase.google.com/docs/unity/setup) | Unity 클라이언트 Firestore 연동 | Apache 2.0 |
| [Yarn Spinner for Unity](https://yarnspinner.dev/) | 대화 시나리오 스크립팅 | MIT |
| [Unity Animation Rigging](https://docs.unity3d.com/Packages/com.unity.animation.rigging@latest) | 골렘 IK / 리깅 애니메이션 | Unity Package |
| [Unity New Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest) | 키보드 / 마우스 / 컨트롤러 입력 처리 | Unity Package |
| [Pydantic / pydantic-settings](https://docs.pydantic.dev/) | AI 서버 설정 및 DTO 유효성 검사 | MIT |
| [Uvicorn](https://www.uvicorn.org/) | ASGI 웹 서버 (FastAPI 실행) | BSD |
| [python-dotenv](https://github.com/theskumar/python-dotenv) | `.env` 환경 변수 로드 | BSD |
| [tenacity](https://github.com/jd/tenacity) | API 호출 재시도 로직 | Apache 2.0 |
| [dependency-injector](https://python-dependency-injector.ets-labs.org/) | AI 서버 DI 컨테이너 | MIT |
| [matplotlib](https://matplotlib.org/) | 평가 결과 시각화(bar chart) | PSF |

---

## 사용 에셋

게임에 사용한 외부 에셋(3D 모델·텍스처·사운드·VFX) 목록입니다. 일부 유료/스토어 에셋은 라이선스상 저장소에 포함하지 않으므로(`.gitignore` 처리) 각자 Unity Asset Store 등에서 직접 임포트해야 합니다.

| 에셋 | 용도 |
|---|---|
| [Footsteps Pack Expanded](http://assetstore.unity.com/packages/audio/sound-fx/foley/footsteps-pack-330509) | 주인공 · 골렘 발소리 (Room 포함) |
| [In-game Debug Console](https://assetstore.unity.com/packages/tools/gui/in-game-debug-console-68068#content) | 인게임 디버그 콘솔 |
| [Yughues Free Wooden Floor Materials](https://assetstore.unity.com/packages/2d/textures-materials/wood/yughues-free-wooden-floor-materials-13213) | Room 바닥 머테리얼 |
| [FarlandSkies - Cloudy Crown](https://assetstore.unity.com/packages/2d/textures-materials/sky/farland-skies-cloudy-crown-60004) | Room · Forest 스카이박스 |
| [Poly Fantasy Pack (polyperfect)](https://assetstore.unity.com/packages/3d/props/poly-fantasy-pack-264883) | Forest · Gesture 씬 자연물 · 다리 · 줄 · 주머니 3D 모델 |
| [Handpainted Grass & Ground Textures](https://assetstore.unity.com/packages/p/handpainted-grass-ground-textures-187634) | Forest terrain 기본 텍스처 |
| [Low Poly Tree Pack](https://assetstore.unity.com/packages/3d/vegetation/trees/low-poly-tree-pack-57866) | Forest 나무 |
| [Low Poly Nature Pack - PVA](https://assetstore.unity.com/packages/3d/environments/low-poly-nature-pack-pva-243603) | Forest 물 셰이더 |
| [Simple Low Poly Nature Pack](https://assetstore.unity.com/packages/3d/environments/landscapes/simple-low-poly-nature-pack-157552) | Forest 돌 |
| [Low-Poly Simple Nature Pack](https://assetstore.unity.com/packages/3d/environments/landscapes/low-poly-simple-nature-pack-162153#content) | Forest 자연물 (풀 · 버섯 · 바위 등) |
| [Polygonal's Low-Poly Particle Pack](https://assetstore.unity.com/packages/vfx/particles/polygonal-s-low-poly-particle-pack-118355) | Forest 비 파티클 |
| [Elemental Spells Full Pack VFX (PixPlays)](https://assetstore.unity.com/packages/vfx/particles/spells/elemental-spells-full-pack-vfx-297318) | Gesture 씬 바람(WindAura) VFX |
| [Adventure Music & SFX (WhatSoundsNice)](https://assetstore.unity.com/packages/audio/music/adventure-music-and-sfx-221545) | 배경 음악(BGM) |
| [RPG Essentials Sound Effects - FREE! (Leohpaz)](https://assetstore.unity.com/packages/audio/sound-fx/rpg-essentials-sound-effects-free-227708) | 바람 효과음 |

---

## 개발자

이화여자대학교 컴퓨터공학과 캡스톤 디자인 프로젝트

- **강한나** · **박세은** · **최은별**


## 키워드

`#멀티모달AI` `#아동게임` `#어드벤처` `#사회적상호작용`
