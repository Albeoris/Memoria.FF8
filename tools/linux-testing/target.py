#!/usr/bin/env python3
"""Bounded, shell-free Linux test runner. Only the selected staging tree is written."""
import argparse
import glob
import hashlib
import json
import os
import re
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import time
import tempfile
import zipfile
import uuid


def file_manifest(root):
    base = Path(root).resolve()
    return {str(p.relative_to(base)).replace(os.sep, "/"): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted(base.rglob("*")) if p.is_file() and not p.is_symlink()}


def validate_manifest(manifest):
    if not isinstance(manifest, dict):
        raise ValueError("Payload manifest must be an object")
    for name, digest in manifest.items():
        parts = name.split("/")
        if not name or "\\" in name or ":" in name or any(part in ("", ".", "..") for part in parts) or not isinstance(digest, str) or not re.fullmatch("[0-9a-f]{64}", digest):
            raise ValueError("Unsafe payload manifest path or digest: " + name)


def atomic_json(path, value):
    temporary = path.with_name(path.name + "." + uuid.uuid4().hex + ".tmp")
    if path.is_symlink() or temporary.is_symlink():
        raise ValueError("Deployment metadata cannot be a symlink")
    temporary.write_text(json.dumps(value))
    os.replace(temporary, path)


def checked_manifest(directory):
    if directory.is_symlink() or any(path.is_symlink() for path in directory.rglob("*")):
        raise ValueError("Staging symlinks are unsupported; select a fresh TargetDirectory")
    return file_manifest(directory)


def reconcile(root, incoming):
    root = Path(root).expanduser().resolve()
    payload = root / "payload"
    receipt = root / "payload-manifest.json"
    if receipt.is_symlink():
        raise ValueError("Payload receipt cannot be a symlink")
    previous = json.loads(receipt.read_text()) if receipt.exists() else {}
    validate_manifest(incoming)
    validate_manifest(previous)
    actual = checked_manifest(payload)
    for name, digest in actual.items():
        if name not in previous or digest != previous[name]:
            raise ValueError("Staging contains an unknown or edited file; preserve it and select a fresh TargetDirectory: " + name)
    payload.mkdir(parents=True, exist_ok=True)
    return actual


def recover_deployment(root):
    journal_path = root / "deployment.json"
    if not journal_path.exists():
        return
    if journal_path.is_symlink():
        raise ValueError("Deployment journal cannot be a symlink")
    journal = json.loads(journal_path.read_text())
    previous, incoming = journal["previous"], journal["incoming"]
    validate_manifest(previous)
    validate_manifest(incoming)
    payload, stage, backup = root / "payload", root / "deployment-staging", root / "deployment-backup"
    actual = checked_manifest(payload)
    if backup.exists():
        if checked_manifest(backup) != previous:
            raise ValueError("Deployment backup was edited; all files are preserved")
        if not payload.exists():
            if checked_manifest(stage) != incoming:
                os.replace(backup, payload)
                raise ValueError("Interrupted deployment staging is incomplete; original payload restored")
            os.replace(stage, payload)
            actual = incoming
        if actual != incoming:
            raise ValueError("Interrupted deployment payload was edited; all files are preserved")
        atomic_json(root / "payload-manifest.json", incoming)
        retired = root / ("deployment-retired-" + uuid.uuid4().hex)
        os.replace(backup, retired)
        journal_path.unlink()
        shutil.rmtree(retired)
    elif actual == incoming and not stage.exists():
        atomic_json(root / "payload-manifest.json", incoming)
        journal_path.unlink()
    elif actual != previous:
        raise ValueError("Pending deployment contains an edited active payload; all files are preserved")


def prepare_deployment(root, incoming):
    root = Path(root).expanduser().resolve()
    validate_manifest(incoming)
    recover_deployment(root)
    actual = reconcile(root, incoming)
    receipt = root / "payload-manifest.json"
    previous = json.loads(receipt.read_text()) if receipt.exists() else {}
    journal_path, stage = root / "deployment.json", root / "deployment-staging"
    if journal_path.exists():
        journal = json.loads(journal_path.read_text())
        if journal["incoming"] != incoming:
            pending = checked_manifest(stage)
            if any(name not in journal["incoming"] or digest != journal["incoming"][name] for name, digest in pending.items()):
                raise ValueError("Pending deployment staging was edited; all files are preserved")
            abandoned = root / ("deployment-abandoned-" + uuid.uuid4().hex)
            if stage.exists():
                os.replace(stage, abandoned)
            os.replace(journal_path, abandoned.with_suffix(".json"))
    if not journal_path.exists():
        if stage.exists() or (root / "deployment-backup").exists():
            raise ValueError("Unowned deployment staging/backup exists; preserve it and select a fresh target directory")
        atomic_json(journal_path, {"previous": previous, "incoming": incoming})
    staged = checked_manifest(stage)
    if any(name not in incoming or digest != incoming[name] for name, digest in staged.items()):
        raise ValueError("Pending deployment staging was edited; all files are preserved")
    stage.mkdir(parents=True, exist_ok=True)
    for name, digest in incoming.items():
        if name not in staged and actual.get(name) == digest:
            destination = stage / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            try:
                os.link(root / "payload" / name, destination)
            except OSError:
                shutil.copy2(root / "payload" / name, destination)
    return checked_manifest(stage)


def accept_deployment_archive(root, incoming, archive_path):
    root = Path(root).expanduser().resolve()
    prepare_deployment(root, incoming)
    inbox = root / "inbox"
    inbox.mkdir(exist_ok=True)
    archive_path = Path(archive_path)
    if archive_path.is_symlink() or archive_path.resolve().parent != inbox.resolve():
        raise ValueError("Deployment archives must be regular files inside the owned inbox")
    with tempfile.TemporaryDirectory(prefix="verified-", dir=inbox) as temporary:
        verified = Path(temporary)
        with zipfile.ZipFile(archive_path) as archive:
            names = set()
            total = 0
            for entry in archive.infolist():
                name = entry.filename
                if name in names or name not in incoming or entry.is_dir() or (entry.external_attr >> 16) & 0o170000 == 0o120000:
                    raise ValueError("Unexpected deployment archive entry")
                names.add(name)
                total += entry.file_size
                if entry.file_size > 1024 ** 3 or total > 4 * 1024 ** 3:
                    raise ValueError("Deployment archive exceeds size limits")
                output = verified / name
                output.parent.mkdir(parents=True, exist_ok=True)
                with archive.open(entry) as source, output.open("wb") as destination:
                    shutil.copyfileobj(source, destination)
                if hashlib.sha256(output.read_bytes()).hexdigest() != incoming[name]:
                    raise ValueError("Deployment archive content hash mismatch")
        for name in names:
            destination = root / "deployment-staging" / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            os.replace(verified / name, destination)
    archive_path.unlink()


def commit_deployment(root, incoming):
    root = Path(root).expanduser().resolve()
    validate_manifest(incoming)
    recover_deployment(root)
    journal_path = root / "deployment.json"
    if not journal_path.exists():
        if checked_manifest(root / "payload") == incoming:
            return
        raise ValueError("No pending deployment matches the requested commit")
    journal = json.loads(journal_path.read_text())
    if journal["incoming"] != incoming or checked_manifest(root / "deployment-staging") != incoming:
        raise ValueError("Deployment is incomplete or its content hashes do not match; active payload preserved")
    if checked_manifest(root / "payload") != journal["previous"]:
        raise ValueError("Active payload was edited before deployment; all files are preserved")
    os.replace(root / "payload", root / "deployment-backup")
    os.replace(root / "deployment-staging", root / "payload")
    recover_deployment(root)


def graphics_diagnostics(output, environment, mode):
    descriptors = sorted(set(glob.glob("/usr/share/vulkan/icd.d/*.json") + glob.glob("/etc/vulkan/icd.d/*.json")))
    inventory = []
    for path in descriptors:
        try:
            inventory.append({"path": path, "descriptor": json.loads(Path(path).read_text())})
        except (OSError, ValueError) as error:
            inventory.append({"path": path, "error": str(error)})
    (output / "vulkan-icds.json").write_text(json.dumps(inventory, indent=2))
    result = {"requested": mode, "classification": "not tested", "presentation": "not tested", "memoriaVulkanAccepted": False}
    if mode == "None":
        return result
    if mode == "Software":
        software = [item["path"] for item in inventory if "lvp" in json.dumps(item.get("descriptor", {})).lower()]
        if len(software) != 1:
            result["error"] = "Software Vulkan needs exactly one installed Lavapipe ICD descriptor; use explicit Ubuntu dependency setup."
            return result
        environment["VK_ICD_FILENAMES"] = software[0]
        environment["VK_DRIVER_FILES"] = software[0]
        result["forcedIcd"] = software[0]
    if not shutil.which("vulkaninfo"):
        result["error"] = "vulkaninfo is unavailable; install normal distro vulkan-tools explicitly."
        return result
    summary_code = capture(["vulkaninfo", "--summary"], output / "vulkan-summary.txt", environment)
    capture(["vulkaninfo"], output / "vulkan-full.txt", environment, timeout=25)
    summary = (output / "vulkan-summary.txt").read_text(errors="replace").lower()
    if "physical_device_type_discrete_gpu" in summary or "physical_device_type_integrated_gpu" in summary:
        result["classification"] = "hardware"
    elif "llvmpipe" in summary or "lavapipe" in summary or "physical_device_type_cpu" in summary:
        result["classification"] = "software"
    else:
        result["classification"] = "unverified"
    result["summaryExitCode"] = summary_code
    devices = []
    for match in re.finditer(r"gpu(\d+):([\s\S]*?)(?=gpu\d+:|\Z)", summary):
        hardware = "physical_device_type_discrete_gpu" in match[2] or "physical_device_type_integrated_gpu" in match[2]
        devices.append({"index": int(match[1]), "hardware": hardware})
    result["devices"] = devices
    if mode == "Hardware" and result["classification"] != "hardware":
        result["error"] = "Hardware Vulkan requested, but no hardware device was identified."
        return result
    if shutil.which("vkcube") and (environment.get("DISPLAY") or environment.get("WAYLAND_DISPLAY")):
        candidates = [device["index"] for device in devices if device["hardware"]]
        if mode == "Hardware" and not candidates:
            result["error"] = "Hardware device index could not be established for the presentation probe."
            return result
        command = ["vkcube", "--c", "30"]
        if candidates and mode != "Software":
            result["probeGpuIndex"] = candidates[0]
            command += ["--gpu_number", str(candidates[0])]
        code = capture(command, output / "vulkan-presentation.txt", environment, timeout=25)
        result["presentation"] = "passed" if code == 0 else "failed"
        result["presentationExitCode"] = code
    else:
        result["presentation"] = "unavailable: vkcube or graphical session missing"
    return result


def portable_tests(payload, root, output, environment, fingerprint):
    receipt = root / "test-results" / (fingerprint + ".json")
    if receipt.is_file() and json.loads(receipt.read_text()).get("passed"):
        return {"status": "cached", "fingerprint": fingerprint}
    results = {}
    for name in ("Memoria.FF8.Platform.Tests", "Memoria.FF8.Audio.Tests", "Memoria.Launcher.Tests", "Memoria.FF8.Presentation.Tests"):
        executable = payload / "tests" / name / name
        if not executable.is_file():
            raise ValueError("Portable test executable missing: " + name)
        executable.chmod(executable.stat().st_mode | 0o111)
        code = capture([str(executable), "--progress", "off"], output / (name + ".log"), environment, timeout=180)
        results[name] = code
    passed = all(code == 0 for code in results.values())
    receipt.parent.mkdir(parents=True, exist_ok=True)
    receipt.write_text(json.dumps({"passed": passed, "results": results, "fingerprint": fingerprint}))
    return {"status": "passed" if passed else "failed", "results": results, "fingerprint": fingerprint}


class OwnedProcessGroup:
    def __init__(self, group_id, token):
        self.group_id = group_id
        self.token = token
        self.identities = set()
        self.members()

    def members(self):
        members = []
        verified = False
        for path in Path("/proc").iterdir():
            if not path.name.isdecimal():
                continue
            try:
                fields = (path / "stat").read_text().split(") ", 1)[1].split()
                if int(fields[2]) != self.group_id or int(fields[3]) != self.group_id or fields[0] == "Z":
                    continue
                identity = (int(path.name), fields[19])
                try:
                    environment = (path / "environ").read_bytes().split(b"\0")
                except PermissionError:
                    environment = []
                owned = identity in self.identities or ("MEMORIA_LINUX_TEST_GROUP_ID=" + self.token).encode() in environment
                verified |= owned
                members.append(identity)
            except (FileNotFoundError, ProcessLookupError, PermissionError):
                continue
        if members and not verified:
            raise RuntimeError("Process group ownership cannot be verified; refusing to signal unrelated processes")
        self.identities.update(members)
        return members

    def signal(self, value):
        if self.members():
            try:
                os.killpg(self.group_id, value)
            except ProcessLookupError:
                pass


def stop_owned_group(group, grace_seconds=5):
    group.signal(signal.SIGTERM)
    deadline = time.monotonic() + grace_seconds
    while group.members() and time.monotonic() < deadline:
        time.sleep(0.05)
    if group.members():
        group.signal(signal.SIGKILL)
        deadline = time.monotonic() + 2
        while group.members() and time.monotonic() < deadline:
            time.sleep(0.05)
        if group.members():
            raise RuntimeError("Owned process group did not terminate after SIGKILL")


def stop(process):
    # Popen.poll only describes the leader; the retained group also owns descendants after it exits.
    stop_owned_group(process.memoria_group)
    process.wait(timeout=5)


def start_owned(command, environment, output, **options):
    child_environment = environment.copy()
    token = uuid.uuid4().hex
    child_environment["MEMORIA_LINUX_TEST_GROUP_ID"] = token
    process = subprocess.Popen(command, env=child_environment, start_new_session=True, **options)
    process.memoria_group = OwnedProcessGroup(process.pid, token)
    process.memoria_marker = output / ("group-" + str(process.pid) + ".json")
    atomic_json(process.memoria_marker, {"group": process.pid, "token": token})
    return process


def capture(command, output, environment, timeout=15):
    with output.open("w", encoding="utf-8") as log:
        try:
            process = start_owned(command, environment, output.parent, stdout=log, stderr=subprocess.STDOUT, cwd=output.parent)
        except OSError as error:
            log.write(str(error))
            return None
        try:
            return process.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            log.write("\nDiagnostic timed out.\n")
            return None
        finally:
            stop(process)
            process.memoria_marker.unlink(missing_ok=True)


def run(request):
    root = Path(request["root"]).expanduser().resolve()
    payload = root / "payload"
    run_name = request["runId"]
    if not run_name.replace("-", "").isalnum():
        raise ValueError("Invalid run identifier")
    output = root / "runs" / run_name
    output.mkdir(parents=True, exist_ok=False)
    environment = os.environ.copy()
    environment["MEMORIA_LINUX_TEST_RUN_ID"] = run_name
    environment.update(request.get("graphicalEnvironment", {}))
    runtime = environment.get("XDG_RUNTIME_DIR", "/run/user/" + str(os.getuid()))
    environment["XDG_RUNTIME_DIR"] = runtime
    environment.setdefault("DBUS_SESSION_BUS_ADDRESS", "unix:path=" + runtime + "/bus")
    if not environment.get("WAYLAND_DISPLAY") and not environment.get("DISPLAY"):
        sockets = [p for p in glob.glob(runtime + "/wayland-*") if not p.endswith(".lock")]
        if sockets:
            environment["WAYLAND_DISPLAY"] = Path(sockets[0]).name
        elif Path("/tmp/.X11-unix/X0").exists():
            environment["DISPLAY"] = ":0"
    # Keep launcher settings/checkpoints separate from the user's normal application data.
    for name, relative in [("XDG_CONFIG_HOME", "config"), ("XDG_DATA_HOME", "data"), ("XDG_STATE_HOME", "state"), ("XDG_CACHE_HOME", "cache")]:
        environment[name] = str(root / "state" / relative)
    result = {"schemaVersion": 1, "mode": request["mode"], "component": request["component"],
              "gameplayAccepted": False, "nativeGpuAcceptance": False, "exitCode": 1,
              "boundary": "Launcher/audio validation only; portable Linux game host unavailable.", "runId": run_name}
    (output / "system.json").write_text(json.dumps({"platform": dict(zip(("sysname", "nodename", "release", "version", "machine"), os.uname())), "display": environment.get("DISPLAY"), "wayland": environment.get("WAYLAND_DISPLAY"), "runtime": runtime}, indent=2))
    def cancel(signum, frame):
        raise KeyboardInterrupt()
    signal.signal(signal.SIGTERM, cancel)
    signal.signal(signal.SIGHUP, cancel)
    runner_marker = output / "runner.json"
    start_time = Path("/proc/self/stat").read_text().split(") ", 1)[1].split()[19]
    runner_marker.write_text(json.dumps({"pid": os.getpid(), "startTime": start_time}))
    try:
        result["graphics"] = graphics_diagnostics(output, environment, request.get("vulkan", "Auto"))
        capture(["uname", "-a"], output / "kernel.txt", environment)
        if request.get("vulkan") in ("Software", "Hardware") and result["graphics"].get("error"):
            raise ValueError(result["graphics"]["error"])
        if request.get("diagnosticsOnly"):
            result["portableTests"] = {"status": "not requested"}
            result["exitCode"] = 0
            return 0
        expected = json.loads((root / "payload-manifest.json").read_text())
        actual = file_manifest(payload)
        if actual != expected:
            raise ValueError("Staged payload hashes do not match the verified build manifest.")
        result["portableTests"] = portable_tests(payload, root, output, environment, request["fingerprint"])
        if result["portableTests"]["status"] == "failed":
            for name, code in result["portableTests"]["results"].items():
                if code != 0:
                    print((output / (name + ".log")).read_text(errors="replace")[-4000:])
            raise ValueError("Portable tests failed; inspect per-project test logs.")
        component = request["component"]
        if component == "Game" and not request.get("gameHost"):
            raise ValueError("Linux gameplay is unavailable until a supported game distribution recipe is committed.")
        if component == "Audio":
            for key in ("dlsPath", "sgtPath"):
                if not request.get(key) or not Path(request[key]).is_file():
                    raise ValueError("Audio requires explicitly configured existing dlsPath and sgtPath fixtures.")
            command = [str(payload / "audio" / "Memoria.FF8.Music"), "--play", request["dlsPath"], request["sgtPath"], str(request.get("audioSeconds", 10))]
        elif component == "Game":
            if not request.get("gameDirectory") or not Path(request["gameDirectory"]).is_dir():
                raise ValueError("Select an existing legal game data directory.")
            command = [str(payload / request["gameExecutable"]), "--game-dir", request["gameDirectory"]]
            result["boundary"] = "Game process smoke; gameplay/native GPU acceptance requires human inspection."
        else:
            command = [str(payload / "Memoria.Launcher")]
            result["boundary"] = "Launcher process smoke; browse/dialog/render acceptance requires human inspection. Linux game host unavailable."
        if component != "Audio" and not (environment.get("DISPLAY") or environment.get("WAYLAND_DISPLAY")):
            raise ValueError("No existing graphical session found. Start WSLg or configure the hardware desktop session environment.")
        if request.get("gameDirectory") and not Path(request["gameDirectory"]).is_dir():
            raise ValueError("Configured game directory is not accessible. For Windows drives, explicitly mount/expose the drive to WSL and retry.")
        if component == "Launcher" and request.get("gameDirectory"):
            settings_path = Path(environment["XDG_CONFIG_HOME"]) / "Memoria" / "FF8" / "launcher.json"
            settings_path.parent.mkdir(parents=True, exist_ok=True)
            settings_path.write_text(json.dumps({"LastGameDirectory": request["gameDirectory"], "AchievementsLocalOnly": True}))
        screenshot_command = request.get("screenshotCommand", [])
        if not screenshot_command:
            if environment.get("WAYLAND_DISPLAY") and shutil.which("grim"):
                screenshot_command = ["grim", "{output}"]
            elif shutil.which("spectacle"):
                screenshot_command = ["spectacle", "-b", "-n", "-o", "{output}"]
            elif environment.get("DISPLAY") and shutil.which("gnome-screenshot"):
                screenshot_command = ["gnome-screenshot", "-f", "{output}"]
        result["screenshot"] = "pending" if screenshot_command else "unavailable: configure an existing ScreenshotCommand"
        for executable in [payload / "Memoria.Launcher", payload / "audio" / "Memoria.FF8.Music"]:
            if executable.is_file():
                executable.chmod(executable.stat().st_mode | 0o111)
        # Native libraries produced on Windows may lose the Unix executable bit; only ELF apphosts need it.
        timeout = int(request["timeoutSeconds"])
        process = None
        with (output / "process.log").open("w", encoding="utf-8") as log:
            started = time.monotonic()
            process = start_owned(command, environment, output, cwd=payload, stdout=log, stderr=subprocess.STDOUT)
            (output / "leader-pid").write_text(str(process.pid))
            try:
                while process.poll() is None and time.monotonic() - started < timeout:
                    screenshot = screenshot_command
                    if screenshot and time.monotonic() - started > 3 and not (output / "screenshot-attempt.txt").exists():
                        (output / "screenshot-attempt.txt").write_text("Configured screenshot command invoked; inspect screenshot.png manually.")
                        screenshot_code = capture([part.replace("{output}", str(output / "screenshot.png")) for part in screenshot], output / "screenshot.log", environment)
                        result["screenshot"] = "captured" if screenshot_code == 0 and (output / "screenshot.png").is_file() else "failed: inspect screenshot.log"
                    time.sleep(0.2)
                result["elapsedSeconds"] = round(time.monotonic() - started, 2)
                if process.poll() is None:
                    result["processSurvivedSmoke"] = True
                    result["exitCode"] = 0 if request["smoke"] else 124
                    result["termination"] = "smoke deadline" if request["smoke"] else "launch timeout"
                else:
                    result["exitCode"] = process.returncode if component == "Audio" or not request["smoke"] else (process.returncode or 1)
                    if component != "Audio" and request["smoke"] and process.returncode == 0:
                        result["error"] = "Launcher/game exited before the smoke interval; sustained startup was not established."
                    result["processSurvivedSmoke"] = False
                    result["termination"] = "process exit"
                    if result["exitCode"] != 0:
                        result["errorTail"] = (output / "process.log").read_text(errors="replace")[-4000:]
            finally:
                stop(process)
                process.memoria_marker.unlink(missing_ok=True)
    except KeyboardInterrupt:
        result["exitCode"] = 130
        result["termination"] = "cancelled"
    except Exception as error:
        result["exitCode"] = 1
        result["error"] = str(error)
    finally:
        runner_marker.unlink(missing_ok=True)
        (output / "result.json").write_text(json.dumps(result, indent=2))
        print(json.dumps({"output": str(output), "result": result}))
    return result["exitCode"]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("operation", choices=["run", "manifest", "mkdir", "probe", "stop", "sync", "plan", "accept", "commit"])
    parser.add_argument("value", nargs="?")
    parser.add_argument("--archive")
    args = parser.parse_args()
    if args.operation in ("sync", "plan", "accept", "commit"):
        data = json.loads(Path(args.value).read_text())
        root = Path(data["root"]).expanduser().resolve()
        incoming = data["manifest"]
        if args.operation == "commit":
            commit_deployment(root, incoming)
            return 0
        if args.operation == "accept":
            accept_deployment_archive(root, incoming, args.archive)
            return 0
        staged = prepare_deployment(root, incoming)
        if args.operation == "plan":
            print(json.dumps(staged))
            return 0
        source = Path(data["source"])
        inbox = root / "inbox"
        inbox.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="copy-", dir=inbox) as temporary:
            for name, digest in incoming.items():
                if staged.get(name) != digest:
                    copied = Path(temporary) / "file"
                    shutil.copy2(source / name, copied)
                    if hashlib.sha256(copied.read_bytes()).hexdigest() != digest:
                        raise ValueError("Source changed during deployment; active payload preserved")
                    destination = root / "deployment-staging" / name
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    os.replace(copied, destination)
        commit_deployment(root, incoming)
        return 0
    if args.operation == "stop":
        run_dir = Path(args.value).expanduser()
        for group_marker in run_dir.glob("group-*.json"):
            data = json.loads(group_marker.read_text())
            try:
                stop_owned_group(OwnedProcessGroup(int(data["group"]), data["token"]))
            except RuntimeError as error:
                print(str(error), file=sys.stderr)
        runner = run_dir / "runner.json"
        if runner.is_file():
            data = json.loads(runner.read_text())
            pid = int(data["pid"])
            try:
                stat = Path("/proc/" + str(pid) + "/stat").read_text().split(") ", 1)[1].split()[19]
                cmdline = Path("/proc/" + str(pid) + "/cmdline").read_bytes().split(b"\0")
                expected_script = str(run_dir.parents[1] / "target.py").encode()
                if stat == data["startTime"] and expected_script in cmdline:
                    os.kill(pid, signal.SIGTERM)
            except (ProcessLookupError, FileNotFoundError):
                pass
        return 0
    if args.operation == "manifest":
        print(json.dumps(file_manifest(Path(args.value).expanduser())))
        return 0
    if args.operation == "mkdir":
        Path(args.value).expanduser().mkdir(parents=True, exist_ok=True)
        return 0
    if args.operation == "probe":
        print(json.dumps({"system": dict(zip(("sysname", "nodename", "release", "version", "machine"), os.uname())), "python": sys.version, "display": os.environ.get("DISPLAY"), "wayland": os.environ.get("WAYLAND_DISPLAY"), "wslg": Path("/mnt/wslg").is_dir()}))
        return 0
    return run(json.loads(Path(args.value).read_text()))


if __name__ == "__main__":
    sys.exit(main())
