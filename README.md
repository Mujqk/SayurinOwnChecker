# <div align="center"> <img src="https://capsule-render.vercel.app/api?type=waving&color=0:6a1b9a,100:1a1a2e&height=180&section=header&text=SAYURIN%20CHECKER&fontSize=55&fontColor=ffffff&fontAlignY=38&desc=Advanced%20System%20Forensics%20for%20Baza%20CS2&descColor=e1bee7&descAlignY=58" /> </div>

<div align="center">

[![Website](https://img.shields.io/badge/Сайт-Скачать_С_Сайта-purple?style=for-the-badge&logo=googlechrome&logoColor=white&labelColor=121212)](https://mujqk.github.io/SayurinOwnChecker/)
[![Releases](https://img.shields.io/badge/Release-Последние_Релизы-green?style=for-the-badge&logo=github&labelColor=121212)](https://github.com/Mujqk/SayurinOwnChecker/releases)
![Framework](https://img.shields.io/badge/.NET-9.0-512bd4?style=for-the-badge&logo=dotnet&labelColor=121212)
![Language](https://img.shields.io/badge/Language-C%23-239120?style=for-the-badge&logo=csharp&labelColor=121212)

> **⚠️ Важно:** Для корректной работы всех модулей (анализ реестра, драйверов и USB) необходим запуск от имени **Администратора**.
</div>

---
## ⚡ О проекте

**Sayurin Checker** — это специализированное десктопное приложение для глубокого анализа системы (PC Check), разработанное на платформе **.NET 9 (C#)** специально для проекта **Baza CS2**.

Инструмент спроектирован для предоставления администраторам серверов консолидированного forensic-интерфейса, объединяющего возможности мониторинга файловой системы, реестра, периферии и игровых данных. Это не автоматический «античит», а профессиональный ассистент для ручной и полуавтоматической проверки игроков.

---

## 🔍 Основной функционал

### 🛡️ PC Проверка и Реестр
* **Глубокое сканирование:** Высокопроизводительный поиск подозрительных файлов (`.exe`, `.dll`, `.sys`, `.bin`) с фильтрацией по дате изменения и размеру.
* **Registry Navigation:** Мгновенный доступ к критическим веткам реестра и поиск следов удаленного или скрытого ПО.
* **16+ Интегрированных утилит:** Панель быстрого запуска внешних инструментов форензики (*Everything, Shellbag Analyzer, UserAssist, System Informer* и др.) без необходимости их предварительной установки.

### 🎮 Игровая интеграция (CS2 / Steam)
* **Steam Account Lookup:** Автоматическое обнаружение активного Steam-аккаунта, проверка **VAC-статуса** и прямой переход в профиль.
* **Baza API:** Синхронизация с базой проекта для проверки истории банов и мутов в реальном времени.
* **Input Analysis:** Сканирование и анализ конфигурационных файлов CS2 на наличие запрещенных биндов и сочетаний клавиш.

### 💾 Мониторинг USB & Периферии
* **USB Timeline:** Подробная история подключений внешних накопителей и периферийных девайсов.
* **Time Tracking:** Логирование точного времени **подключения и отключения** устройств.
* **HWID Data:** Уникальный идентификатор устройства, Vendor ID (VID) и серийные номера для выявления подмены оборудования.

### 🛠️ Системная экспертиза
* **Fast Paths:** Мгновенный переход к `AppData`, `Prefetch`, `CrashDumps` и `Recent` в один клик.
* **Advanced Tools:** Доступ к управлению службами, сетевым параметрам и панели NVIDIA прямо из интерфейса.
* **VM Detector:** Встроенный эвристический алгоритм определения запуска в виртуальной среде.

## 📊 Технические характеристики

| Параметр | Значение |
| :--- | :--- |
| **Ядро / Стек** | **.NET 9.0 (C#)** |
| **Режим запуска** | Admin Mode Required |
| **ОС** | Windows 10 / 11 (включая LTSC / IoT) |
| **Интеграция** | Steam Web API, Baza API |

---

## 📂 Структура проекта

```text
SAYURIN
├── 🔍 PC ПРОВЕРКА   — Анализ файлов и поиск подозрительных объектов
├── 📑 РЕЕСТР        — Сканирование путей и следов программ
├── 👤 АККАУНТЫ     — Статус Steam, VAC и локальные баны Baza
├── 🔌 USB           — История подключений накопителей и периферии
├── ⚙️ ПРОГРАММЫ    — Панель запуска внешних утилит и буферы
├── 📂 ПАПКИ         — Быстрые переходы к системным директориям
├── 🌐 САЙТЫ         — Быстрый доступ к почтам и сервисам оплаты
└── 🖥️ СИСТЕМА       — ТТХ железа и детектор Виртуальных Машин
```
---

<div align="center">
  
  <img src="https://capsule-render.vercel.app/api?type=rect&color=0:6a1b9a,100:1a1a2e&height=2&section=footer" width="100%" />

  <br>

  <h1>
    <img src="https://raw.githubusercontent.com/Tarikul-Islam-Anik/Animated-Fluent-Emojis/master/Emojis/Smilies/Purple%20Heart.png" alt="Purple Heart" width="35" />
    Developed by <ins>NazeSayurin?</ins>
  </h1>

  <p align="center">
    <kbd>C# Enthusiast</kbd> • <kbd>.NET 9 Architect</kbd> • <kbd>OSU! Relax Master</kbd>
  </p>

  <div align="center">
    <img src="https://img.shields.io/badge/Maintained-Yes-6a1b9a?style=for-the-badge&logo=github&labelColor=121212" />
    <img src="https://img.shields.io/badge/Vibe-Low_cortisol-9c27b0?style=for-the-badge&logo=visualstudiocode&labelColor=121212" />
    <img src="https://img.shields.io/badge/Coffee_Level-Critical-red?style=for-the-badge&logo=coffeescript&labelColor=121212" />
  </div>

  <br>

  <img src="https://capsule-render.vercel.app/api?type=waving&color=1a1a2e&height=120&section=footer&text=SEE%20YOU%20IN%20BAZA%20CS2&fontSize=30&fontColor=6a1b9a&fontAlignY=60" width="100%" />

  <p align="center"><i>"Когда берешься за дело и ничего не получаеться, то делай до того момента пока не получиться."</i></p>

</div>
