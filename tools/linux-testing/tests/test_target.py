import importlib.util
import json
import hashlib
import os
import sys
import zipfile
import subprocess
from pathlib import Path
import tempfile
import unittest
from unittest import mock

spec = importlib.util.spec_from_file_location("target", Path(__file__).parents[1] / "target.py")
target = importlib.util.module_from_spec(spec)
spec.loader.exec_module(target)


class TargetTests(unittest.TestCase):
    def test_unknown_target_files_are_preserved(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "payload").mkdir()
            user = root / "payload" / "user.txt"
            user.write_text("preserve")
            with self.assertRaisesRegex(ValueError, "unknown or edited"):
                target.reconcile(root, {})
            self.assertEqual("preserve", user.read_text())

    def test_owned_stale_file_removed_but_edited_file_preserved(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "payload").mkdir()
            owned = root / "payload" / "owned"
            owned.write_text("original")
            (root / "payload-manifest.json").write_text(json.dumps(target.file_manifest(root / "payload")))
            owned.write_text("user change")
            with self.assertRaises(ValueError):
                target.reconcile(root, {})
            self.assertEqual("user change", owned.read_text())
            owned.write_text("original")
            target.prepare_deployment(root, {})
            self.assertTrue(owned.exists())
            target.commit_deployment(root, {})
            self.assertFalse(owned.exists())

    def test_manifest_uses_content_not_timestamp(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            file = root / "file with spaces"
            file.write_text("a")
            first = target.file_manifest(root)
            file.write_text("b")
            self.assertNotEqual(first, target.file_manifest(root))

    def test_unsafe_incoming_manifest_never_removes_stale_files(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "payload").mkdir()
            owned = root / "payload" / "owned"
            owned.write_text("original")
            (root / "payload-manifest.json").write_text(json.dumps(target.file_manifest(root / "payload")))
            with self.assertRaisesRegex(ValueError, "Unsafe"):
                target.reconcile(root, {"../escape": "a" * 64})
            self.assertTrue(owned.exists())

    def test_unsafe_previous_manifest_never_mutates_payload(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "payload").mkdir()
            owned = root / "payload" / "owned"
            owned.write_text("preserve")
            (root / "payload-manifest.json").write_text(json.dumps({"../outside": "a" * 64}))
            with self.assertRaisesRegex(ValueError, "Unsafe"):
                target.reconcile(root, {})
            self.assertEqual("preserve", owned.read_text())

    def test_symlink_directory_is_rejected_before_mutation(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "payload" / "linked").mkdir(parents=True)
            original = Path.is_symlink
            with mock.patch.object(Path, "is_symlink", lambda path: path.name == "linked" or original(path)):
                with self.assertRaisesRegex(ValueError, "symlinks"):
                    target.reconcile(root, {})

    def test_software_vulkan_uses_exact_existing_lavapipe_icd(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            descriptor = root / "lvp_icd.json"
            descriptor.write_text(json.dumps({"ICD": {"library_path": "libvulkan_lvp.so"}}))
            environment = {"DISPLAY": ":0"}
            def capture(command, output, env, timeout=15):
                output.write_text("deviceType = PHYSICAL_DEVICE_TYPE_CPU llvmpipe")
                return 0
            with mock.patch.object(target.glob, "glob", return_value=[str(descriptor)]), mock.patch.object(target.shutil, "which", return_value="tool"), mock.patch.object(target, "capture", side_effect=capture):
                result = target.graphics_diagnostics(root, environment, "Software")
            self.assertEqual(str(descriptor), environment["VK_ICD_FILENAMES"])
            self.assertEqual("software", result["classification"])
            self.assertEqual("passed", result["presentation"])
            self.assertFalse(result["memoriaVulkanAccepted"])

    def test_hardware_mode_does_not_accept_cpu_vulkan(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            def capture(command, output, env, timeout=15):
                output.write_text("llvmpipe PHYSICAL_DEVICE_TYPE_CPU")
                return 0
            with mock.patch.object(target.glob, "glob", return_value=[]), mock.patch.object(target.shutil, "which", return_value="tool"), mock.patch.object(target, "capture", side_effect=capture):
                result = target.graphics_diagnostics(root, {"DISPLAY": ":0"}, "Hardware")
            self.assertIn("no hardware", result["error"])

    def test_hardware_probe_selects_hardware_index_in_mixed_inventory(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            commands = []
            def capture(command, output, env, timeout=15):
                commands.append(command)
                output.write_text("GPU0:\ndeviceType = PHYSICAL_DEVICE_TYPE_CPU\nGPU1:\ndeviceType = PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU\n")
                return 0
            with mock.patch.object(target.glob, "glob", return_value=[]), mock.patch.object(target.shutil, "which", return_value="tool"), mock.patch.object(target, "capture", side_effect=capture):
                result = target.graphics_diagnostics(root, {"DISPLAY": ":0"}, "Hardware")
            self.assertEqual(1, result["probeGpuIndex"])
            self.assertEqual(["vkcube", "--c", "30", "--gpu_number", "1"], commands[-1])
            self.assertEqual("passed", result["presentation"])

    def test_stop_uses_retained_group_when_leader_is_running(self):
        process = mock.Mock()
        process.poll.return_value = None
        with mock.patch.object(target, "stop_owned_group") as stop:
            target.stop(process)
        stop.assert_called_once_with(process.memoria_group)
        process.wait.assert_called_once_with(timeout=5)

    def test_stop_uses_retained_group_after_leader_exits(self):
        process = mock.Mock()
        process.poll.return_value = 0
        with mock.patch.object(target, "stop_owned_group") as stop:
            target.stop(process)
        stop.assert_called_once_with(process.memoria_group)

    def test_term_ignoring_descendant_is_killed_independently_of_leader(self):
        group = mock.Mock()
        group.members.side_effect = [[(17, "token")], [(17, "token")], [], []]
        with mock.patch.object(target.signal, "SIGKILL", 9, create=True):
            target.stop_owned_group(group, grace_seconds=0)
        self.assertEqual([mock.call(target.signal.SIGTERM), mock.call(9)], group.signal.call_args_list)

    def test_interrupted_transfer_reuses_exact_verified_staging(self):
        with tempfile.TemporaryDirectory() as directory:
            root = self.deployment_root(directory)
            incoming = {"tool": self.digest("new")}
            self.assertEqual({}, target.prepare_deployment(root, incoming))
            archive = self.deployment_archive(root, {"tool": "new"})
            target.accept_deployment_archive(root, incoming, archive)
            self.assertEqual("old", (root / "payload/tool").read_text())
            self.assertEqual(incoming, target.prepare_deployment(root, incoming))
            target.commit_deployment(root, incoming)
            self.assertEqual("new", (root / "payload/tool").read_text())
            self.assertEqual(incoming, json.loads((root / "payload-manifest.json").read_text()))

    def test_truncated_archive_is_retryable_and_does_not_mutate_active_payload(self):
        with tempfile.TemporaryDirectory() as directory:
            root = self.deployment_root(directory)
            incoming = {"tool": self.digest("new")}
            target.prepare_deployment(root, incoming)
            (root / "inbox").mkdir()
            partial = root / "inbox/partial.zip"
            partial.write_bytes(b"incomplete transfer")
            with self.assertRaises(zipfile.BadZipFile):
                target.accept_deployment_archive(root, incoming, partial)
            self.assertEqual("old", (root / "payload/tool").read_text())
            target.accept_deployment_archive(root, incoming, self.deployment_archive(root, {"tool": "new"}))
            target.commit_deployment(root, incoming)
            self.assertEqual("new", (root / "payload/tool").read_text())

    def test_all_archive_hashes_are_verified_before_staging_changes(self):
        with tempfile.TemporaryDirectory() as directory:
            root = self.deployment_root(directory)
            incoming = {"tool": self.digest("new"), "other": self.digest("expected")}
            target.prepare_deployment(root, incoming)
            with self.assertRaisesRegex(ValueError, "hash mismatch"):
                target.accept_deployment_archive(root, incoming, self.deployment_archive(root, {"tool": "new", "other": "wrong"}))
            self.assertEqual({}, target.file_manifest(root / "deployment-staging"))
            self.assertEqual("old", (root / "payload/tool").read_text())

    def test_interrupted_directory_promotion_recovers_and_commits(self):
        with tempfile.TemporaryDirectory() as directory:
            # Exercise canonicalization even on hosts without a temporary-directory symlink.
            alias_parent = Path(directory) / "alias-parent"
            alias_parent.mkdir()
            root = self.deployment_root(alias_parent / "..")
            incoming = {"tool": self.digest("new")}
            target.prepare_deployment(root, incoming)
            target.accept_deployment_archive(root, incoming, self.deployment_archive(root, {"tool": "new"}))
            original = os.replace
            def interrupt(source, destination):
                if Path(source) == root / "deployment-staging":
                    raise OSError("interrupted after backup rename")
                original(source, destination)
            with mock.patch.object(target.os, "replace", side_effect=interrupt):
                with self.assertRaises(OSError):
                    target.commit_deployment(root, incoming)
            self.assertTrue((root / "deployment-backup").is_dir())
            self.assertTrue((root / "deployment-staging").is_dir())
            self.assertFalse((root / "payload").exists())
            target.recover_deployment(root)
            self.assertEqual("new", (root / "payload/tool").read_text())
            self.assertEqual(incoming, json.loads((root / "payload-manifest.json").read_text()))
            self.assertFalse((root / "deployment.json").exists())

    def test_edited_pending_file_is_preserved_and_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = self.deployment_root(directory)
            incoming = {"tool": self.digest("new")}
            target.prepare_deployment(root, incoming)
            (root / "deployment-staging/tool").write_text("user edit")
            with self.assertRaisesRegex(ValueError, "was edited"):
                target.prepare_deployment(root, incoming)
            self.assertEqual("user edit", (root / "deployment-staging/tool").read_text())
            self.assertEqual("old", (root / "payload/tool").read_text())

    def test_new_build_after_interrupted_transfer_preserves_pending_and_can_deploy(self):
        with tempfile.TemporaryDirectory() as directory:
            root = self.deployment_root(directory)
            first = {"tool": self.digest("new")}
            target.prepare_deployment(root, first)
            target.accept_deployment_archive(root, first, self.deployment_archive(root, {"tool": "new"}))
            second = {"tool": self.digest("latest")}
            self.assertEqual({}, target.prepare_deployment(root, second))
            self.assertEqual("old", (root / "payload/tool").read_text())
            self.assertTrue(list(root.glob("deployment-abandoned-*")))
            target.accept_deployment_archive(root, second, self.deployment_archive(root, {"tool": "latest"}))
            target.commit_deployment(root, second)
            self.assertEqual("latest", (root / "payload/tool").read_text())

    def test_cli_plan_accept_commit_and_unchanged_rerun(self):
        with tempfile.TemporaryDirectory() as directory:
            root = self.deployment_root(directory)
            incoming = {"tool": self.digest("new")}
            request = root / "sync.json"
            request.write_text(json.dumps({"root": str(root), "manifest": incoming}))
            def command(operation, *arguments):
                return subprocess.run([sys.executable, target.__file__, operation, str(request), *arguments], check=True, capture_output=True, text=True, timeout=10)
            self.assertEqual({}, json.loads(command("plan").stdout))
            archive = self.deployment_archive(root, {"tool": "new"})
            command("accept", "--archive", str(archive))
            command("commit")
            self.assertEqual(incoming, json.loads(command("plan").stdout))
            command("commit")
            self.assertEqual("new", (root / "payload/tool").read_text())

    def test_reused_unrelated_process_group_identity_is_never_signalled(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            process = root / "42"
            process.mkdir()
            def stat(start):
                return "42 (owned) " + " ".join(["S", "1", "42", "42"] + ["0"] * 15 + [start])
            (process / "stat").write_text(stat("123"))
            (process / "environ").write_bytes(b"MEMORIA_LINUX_TEST_GROUP_ID=owned-token\0")
            original = target.Path
            with mock.patch.object(target, "Path", side_effect=lambda value: root if value == "/proc" else original(value)), mock.patch.object(target.os, "killpg", create=True) as kill:
                group = target.OwnedProcessGroup(42, "owned-token")
                (process / "stat").write_text(stat("999"))
                (process / "environ").write_bytes(b"MEMORIA_LINUX_TEST_GROUP_ID=unrelated\0")
                with self.assertRaisesRegex(RuntimeError, "ownership"):
                    group.signal(target.signal.SIGTERM)
                kill.assert_not_called()

    @staticmethod
    def digest(text):
        return hashlib.sha256(text.encode()).hexdigest()

    def deployment_root(self, directory):
        # Match the target's canonical paths, including temporary-directory aliases.
        root = Path(directory).resolve()
        (root / "payload").mkdir()
        (root / "payload/tool").write_text("old")
        (root / "payload-manifest.json").write_text(json.dumps(target.file_manifest(root / "payload")))
        return root

    @staticmethod
    def deployment_archive(root, contents):
        (root / "inbox").mkdir(exist_ok=True)
        archive = root / "inbox/incoming.zip"
        with zipfile.ZipFile(archive, "w") as output:
            for name, content in contents.items():
                output.writestr(name, content)
        return archive


@unittest.skipUnless(sys.platform.startswith("linux"), "Requires Linux /proc and process-group signals")
class LinuxTargetIntegrationTests(unittest.TestCase):
    def test_real_exited_leader_leaves_term_ignoring_child_that_cleanup_kills(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            child_code = "import signal,time; signal.signal(signal.SIGTERM, signal.SIG_IGN); print('ready',flush=True); time.sleep(60)"
            parent_code = "import subprocess,sys; child=subprocess.Popen([sys.executable,'-c',sys.argv[1]],stdout=subprocess.PIPE,text=True); child.stdout.readline(); print(child.pid,flush=True)"
            process = target.start_owned([sys.executable, "-c", parent_code, child_code], os.environ.copy(), output, stdout=subprocess.PIPE, text=True)
            try:
                child_pid = int(process.stdout.readline())
                self.assertEqual(0, process.wait(timeout=5))
                self.assertTrue(any(pid == child_pid for pid, start in process.memoria_group.members()))
                target.stop_owned_group(process.memoria_group, grace_seconds=0.1)
                self.assertEqual([], process.memoria_group.members())
            finally:
                target.stop_owned_group(process.memoria_group, grace_seconds=0.1)
                process.stdout.close()


if __name__ == "__main__":
    unittest.main()
