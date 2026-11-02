// Standalone helper for Collect-MacWindowDiagnostics.sh; Apple SDK only.
import AppKit
import CoreGraphics
import CryptoKit
import Foundation

let fm = FileManager.default
let args = CommandLine.arguments
let limit = 2 * 1024 * 1024
func clean(_ value: String) -> String {
    var text = value.replacingOccurrences(of: fm.homeDirectoryForCurrentUser.path, with: "<HOME>")
    // Avoid sharing arbitrary absolute installation, temporary, mod or resource paths.
    text = text.replacingOccurrences(of: #"(?<![A-Za-z0-9])/(?:Users|Volumes|Applications|private|var|tmp|opt|usr|Library)/[^\n\r\"<>]*"#, with: "<PATH>", options: .regularExpression)
    text = text.replacingOccurrences(of: #"(?i)(token|password|secret|api[_-]?key)\s*[:=]\s*[^\s,;]+"#, with: "$1=<REDACTED>", options: .regularExpression)
    return text
}
func writeJSON(_ value: Any, _ path: String) throws {
    func redact(_ item: Any) -> Any {
        if let text = item as? String { return clean(text) }
        if let object = item as? [String: Any] { return object.mapValues { redact($0) } }
        if let array = item as? [Any] { return array.map { redact($0) } }
        return item
    }
    let data = try JSONSerialization.data(withJSONObject: redact(value), options: [.prettyPrinted, .sortedKeys])
    try data.write(to: URL(fileURLWithPath: path))
}
func append(_ value: String, _ path: String) {
    if !fm.fileExists(atPath: path) { fm.createFile(atPath: path, contents: nil) }
    guard let file = FileHandle(forWritingAtPath: path) else { return }
    defer { try? file.close() }
    file.seekToEndOfFile()
    file.write(Data((clean(value) + "\n").utf8))
}
func json(_ path: String) -> [String: Any]? {
    guard let attr = try? fm.attributesOfItem(atPath: path), let size = attr[.size] as? NSNumber, size.intValue <= limit,
          let data = fm.contents(atPath: path) else { return nil }
    return (try? JSONSerialization.jsonObject(with: data)) as? [String: Any]
}
func run(_ executable: String, _ arguments: [String]) -> String {
    let process = Process()
    process.executableURL = URL(fileURLWithPath: executable)
    process.arguments = arguments
    let pipe = Pipe()
    process.standardOutput = pipe
    process.standardError = FileHandle.nullDevice
    do { try process.run() } catch { return "unavailable" }
    let data = pipe.fileHandleForReading.readDataToEndOfFile()
    process.waitUntilExit()
    return String(decoding: data, as: UTF8.self).trimmingCharacters(in: .whitespacesAndNewlines)
}
func bounds(_ rect: CGRect) -> [String: Double] {
    return ["x": Double(rect.origin.x), "y": Double(rect.origin.y), "width": Double(rect.width), "height": Double(rect.height)]
}
func displays() -> [[String: Any]] {
    return NSScreen.screens.map { screen in
        let id = (screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as! NSNumber).uint32Value
        let physical = CGDisplayScreenSize(id)
        var result: [String: Any] = ["id": id, "appKitFramePointsBottomLeftOrigin": bounds(screen.frame),
            "visibleFramePoints": bounds(screen.visibleFrame), "backingScaleFactor": screen.backingScaleFactor,
            "cgBoundsTopLeftOrigin": bounds(CGDisplayBounds(id)), "physicalMillimeters": ["width": physical.width, "height": physical.height],
            "displayPixels": ["width": CGDisplayPixelsWide(id), "height": CGDisplayPixelsHigh(id)],
            "builtIn": CGDisplayIsBuiltin(id) != 0, "rotation": CGDisplayRotation(id)]
        if let mode = CGDisplayCopyDisplayMode(id) {
            result["currentMode"] = ["width": mode.width, "height": mode.height, "pixelWidth": mode.pixelWidth,
                "pixelHeight": mode.pixelHeight, "refreshRate": mode.refreshRate, "ioFlags": mode.ioFlags] as [String: Any]
        }
        return result
    }
}
func readyFiles(_ host: String) -> [URL] {
    guard let enumerator = fm.enumerator(at: URL(fileURLWithPath: host), includingPropertiesForKeys: nil) else { return [] }
    return enumerator.compactMap { $0 as? URL }.filter { $0.lastPathComponent == "ready.json" || $0.lastPathComponent == "direct-ready.json" }.prefix(20).map { $0 }
}
func preferences(_ output: String, _ phase: String) {
    let directory = fm.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/Memoria/FF8").path
    guard let source = json(directory + "/ui-settings.json") else {
        append("\(phase): ui-settings.json missing, unreadable or too large", output + "/missing.txt")
        return
    }
    let allowed = ["WindowWidth", "WindowHeight", "BorderlessWindow", "VSync", "MsaaSamples", "Graphics", "Display", "Fullscreen"]
    var filtered: [String: Any] = [:]
    for (key, value) in source where allowed.contains(where: { $0.caseInsensitiveCompare(key) == .orderedSame }) {
        if let settings = value as? [String: Any] {
            let subkeys = ["Backend", "MsaaSamples", "VSync", "Width", "Height", "BorderlessWindow"]
            filtered[key] = settings.filter { pair in subkeys.contains { $0.caseInsensitiveCompare(pair.key) == .orderedSame } }
        } else if value is NSNumber { filtered[key] = value }
    }
    try? writeJSON(filtered, output + "/preferences-\(phase).json")
}
func copyLogs(_ output: String, _ host: String, _ phase: String) {
    guard let files = fm.enumerator(at: URL(fileURLWithPath: host), includingPropertiesForKeys: nil) else { return }
    var observed: [String] = []
    var number = 0
    for case let url as URL in files {
        guard ["stdout.log", "stderr.log", "failure.json"].contains(url.lastPathComponent), number < 30,
              let file = FileHandle(forReadingAtPath: url.path) else { continue }
        defer { try? file.close() }
        let data = file.readData(ofLength: limit)
        let text = clean(String(decoding: data, as: UTF8.self))
        let name = "host-\(phase)-\(number)-\(url.lastPathComponent)"
        try? Data(text.utf8).write(to: URL(fileURLWithPath: output + "/" + name))
        observed += text.components(separatedBy: .newlines).filter { $0.contains("swapchain images with size") }
        if data.count == limit { append("\(name): log capped at \(limit) bytes; later records may be missing", output + "/missing.txt") }
        number += 1
    }
    // Direct host launches write MoltenVK output to launcher-stderr instead.
    for name in ["launcher-stdout.log", "launcher-stderr.log"] {
        if let data = fm.contents(atPath: output + "/" + name) {
            observed += String(decoding: data, as: UTF8.self).components(separatedBy: .newlines).filter { $0.contains("swapchain images with size") }
        }
    }
    try? writeJSON(["observedCreationLogLines": observed, "interpretation": "Cumulative creation events up to this phase; last event is not a query of the current drawable. No event means extent unavailable."], output + "/swapchain-\(phase).json")
    if observed.isEmpty { append("\(phase): no native swapchain extent log observed; actual Vulkan extent unavailable", output + "/missing.txt") }
}

do {
    guard args.count >= 3 else { throw NSError(domain: "arguments", code: 1) }
    let mode = args[1], output = args[2]
    if mode == "stream" {
        fm.createFile(atPath: output, contents: nil)
        let file = FileHandle(forWritingAtPath: output)!
        var count = 0
        var truncated = false
        // Line-oriented redaction avoids leaking a home path split across read chunks.
        while let line = readLine(strippingNewline: false) {
            let data = Data(clean(line).utf8)
            if count + data.count <= limit { file.write(data); count += data.count } else { truncated = true }
        }
        if truncated { file.write(Data("\n[Log capped; some output omitted.]\n".utf8)) }
        try file.close()
    } else if mode == "inventory" {
        let root = args[3], executable = args[4]
        try writeJSON(["schemaVersion": 1, "os": run("/usr/bin/sw_vers", []), "architecture": run("/usr/bin/uname", ["-m"]),
            "hardwareModel": run("/usr/sbin/sysctl", ["-n", "hw.model"]), "cpu": run("/usr/sbin/sysctl", ["-n", "machdep.cpu.brand_string"]),
            "memoryBytes": run("/usr/sbin/sysctl", ["-n", "hw.memsize"]), "displays": displays(),
            "moltenVKLogLevelRequested": 3], output + "/system.json")
        var candidates = [URL(fileURLWithPath: executable)]
        if let files = fm.enumerator(at: URL(fileURLWithPath: root), includingPropertiesForKeys: [.isSymbolicLinkKey, .isRegularFileKey], options: [.skipsHiddenFiles]) {
            for case let url as URL in files {
                let name = url.lastPathComponent
                let values = try? url.resourceValues(forKeys: [.isSymbolicLinkKey])
                if values?.isSymbolicLink == true || ["saves", "mods", "cache", "logs"].contains(name.lowercased()) { files.skipDescendants(); continue }
                if ["dll", "dylib"].contains(url.pathExtension) || ["Memoria.FF8", "Memoria.Launcher"].contains(name) || name.hasSuffix(".runtimeconfig.json") { candidates.append(url) }
                if candidates.count >= 150 { break }
            }
        }
        var hashes: [[String: Any]] = []
        for url in candidates {
            guard let values = try? url.resourceValues(forKeys: [.isSymbolicLinkKey, .isRegularFileKey]), values.isSymbolicLink != true, values.isRegularFile == true,
                  let file = FileHandle(forReadingAtPath: url.path) else { continue }
            var hash = SHA256()
            while true { let data = file.readData(ofLength: 1024 * 1024); if data.isEmpty { break }; hash.update(data: data) }
            try? file.close()
            let relative = url.path.hasPrefix(root + "/") ? String(url.path.dropFirst(root.count + 1)) : "launcher-entrypoint"
            hashes.append(["file": relative, "sha256": hash.finalize().map { String(format: "%02x", $0) }.joined()])
        }
        try writeJSON(hashes, output + "/runtime-hashes.json")
        let bundleRoot = URL(fileURLWithPath: executable).deletingLastPathComponent().deletingLastPathComponent()
        if let plist = NSDictionary(contentsOf: bundleRoot.appendingPathComponent("Info.plist")) {
            var version: [String: Any] = [:]
            for key in ["CFBundleShortVersionString", "CFBundleVersion", "CFBundleIdentifier"] { version[key] = plist[key] }
            try writeJSON(version, output + "/bundle-version.json")
        } else { append("Bundle version unavailable for selected executable; use runtime hashes", output + "/missing.txt") }
        append("Managed backbuffer/viewport/client dimensions are initialization-only ready.json data. This collector cannot query live managed dimensions after resize or borderless transitions. Native window bounds include decoration and are not client dimensions.", output + "/missing.txt")
        preferences(output, "before")
    } else if mode == "phase" {
        let host = args[3], phase = args[4], screenshot = args[5] == "yes", launcherPID = Int32(args[6])!
        var pids = Set<Int32>()
        for (index, url) in readyFiles(host).enumerated() {
            if let ready = json(url.path), let pid = ready["processId"] as? NSNumber {
                pids.insert(pid.int32Value)
                try writeJSON(ready, output + "/initial-ready-\(phase)-\(index).json")
                let capabilities = ready["capabilities"] as? [String: Any] ?? [:]
                let missing = ["backBufferWidth", "backBufferHeight", "viewportWidth", "viewportHeight", "clientWidth", "clientHeight"].filter { capabilities[$0] == nil || capabilities[$0] is NSNull }
                if !missing.isEmpty { append("\(phase): installed host does not report initialization fields: \(missing.joined(separator: ", "))", output + "/missing.txt") }
            }
        }
        if readyFiles(host).isEmpty { append("\(phase): no initial host-ready JSON found; managed initialization dimensions unavailable", output + "/missing.txt") }
        // Fall back only to descendants of this launched process, never unrelated game instances.
        let rows = run("/bin/ps", ["-axo", "pid=,ppid=,comm="]).components(separatedBy: .newlines).map { $0.split(maxSplits: 2, whereSeparator: { $0.isWhitespace }).map(String.init) }
        var descendants: Set<Int32> = [launcherPID]
        for _ in 0..<12 {
            for row in rows where row.count == 3 {
                if let pid = Int32(row[0]), let parent = Int32(row[1]), descendants.contains(parent) { descendants.insert(pid) }
            }
        }
        for row in rows where row.count == 3 {
            if let pid = Int32(row[0]), descendants.contains(pid), URL(fileURLWithPath: row[2]).lastPathComponent == "Memoria.FF8" { pids.insert(pid) }
        }
        if pids.isEmpty { append("\(phase): no game process identified; no window captured", output + "/missing.txt") }
        let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] ?? []
        var selected: [[String: Any]] = []
        for window in windows {
            guard let pid = window[kCGWindowOwnerPID as String] as? NSNumber, pids.contains(pid.int32Value),
                  let layer = window[kCGWindowLayer as String] as? NSNumber, layer.intValue == 0,
                  let id = window[kCGWindowNumber as String] as? NSNumber, let geometry = window[kCGWindowBounds as String] as? [String: Any] else { continue }
            var item: [String: Any] = ["pid": pid, "windowID": id, "cgBoundsPointsTopLeftOrigin": geometry]
            if screenshot {
                if CGPreflightScreenCaptureAccess() || CGRequestScreenCaptureAccess() {
                    let path = output + "/game-\(phase)-\(id).png"
                    let status = run("/usr/sbin/screencapture", ["-x", "-o", "-l", id.stringValue, path])
                    item["screenshotSaved"] = fm.fileExists(atPath: path)
                    if let image = NSBitmapImageRep(contentsOfFile: path) { item["screenshotPixels"] = ["width": image.pixelsWide, "height": image.pixelsHigh] }
                    if !fm.fileExists(atPath: path) { append("\(phase): game-window capture failed \(status)", output + "/missing.txt") }
                } else { append("\(phase): Screen Recording permission denied; screenshot omitted", output + "/missing.txt") }
            }
            selected.append(item)
        }
        if selected.isEmpty { append("\(phase): game window bounds unavailable (window hidden, not started, or permission restricted)", output + "/missing.txt") }
        try writeJSON(["phase": phase, "utc": ISO8601DateFormatter().string(from: Date()), "windows": selected, "displays": displays()], output + "/phase-\(phase).json")
        preferences(output, phase)
        copyLogs(output, host, phase)
    } else if mode == "finish" {
        copyLogs(output, args[3], "final")
        append("Log files contain at most 2 MiB each. Paths and common credential assignments are redacted automatically; review for other personal text. Screenshots, if opted in, contain only identified game windows. No desktop capture, saves or game assets are included.", output + "/README.txt")
    } else { throw NSError(domain: "arguments", code: 2) }
} catch {
    fputs("Diagnostics failed: \(clean(String(describing: error)))\n", stderr)
    exit(1)
}
