# Contrato Técnico: IGCodeSender

Este documento describe el contrato, estados e invariantes del servicio `IGCodeSender` usado para el envío secuencial y seguro de G‑Code hacia GRBL.

## Propósito
Encapsular la lógica de envío una‑línea‑en‑vuelo, procesar respuestas (`ok`/`error:`), soportar pausa/resume y proporcionar telemetría (`FilePosition`, `Runtime`, `EstimatedDuration`).

## Interfaz (resumen)

Entradas/propiedades:
* `State: GCodeSenderState` ∈ { `Idle`, `Sending`, `Paused` }.
* `FilePosition: int` índice de línea actual [0..`FileLength`].
* `FileLength: int` cantidad de líneas cargadas.
* `Runtime: TimeSpan` tiempo transcurrido desde `Start()`.
* `EstimatedDuration: TimeSpan` heurística de duración total.
* `PauseOnHold: bool` si `true`, pausa tras línea con `M0`/`M1`/`M30` (después de `ok`).
* `IsSending: bool` atajo `State == Sending`.
* `CurrentLineText: string` contenido textual de la línea en curso.

Eventos:
* `StateChanged(object sender, GCodeSenderState)`.
* `PositionChanged(object sender, int)` nuevo valor de `FilePosition`.
* `ErrorOccurred(object sender, string)` mensaje con contexto (p. ej., `error:...`).

Operaciones:
* `Load(IEnumerable<string> lines)` carga un archivo; setea `FileLength` y reinicia `FilePosition`.
* `Start()` comienza o reanuda el envío (si hay puerto abierto y líneas cargadas).
* `Pause()` detiene el avance manteniendo `FilePosition`.
* `Clear()` descarta el archivo y vuelve a `Idle`.
* `Goto(int lineIndex)` reposiciona (sólo en `Idle` o `Paused`).
* `Dispose()` libera recursos.

## Protocolo de Envío
1. `Load()` guarda las líneas; `State=Idle`, `FilePosition=0`.
2. `Start()`:
   * Requiere `FileLength>0` y puerto abierto.
   * Cambia `State=Sending` y envía la línea `FilePosition` con terminación `\n`.
3. Al recibir `ok`:
   * Incrementa `FilePosition++` y actualiza `Runtime` y `EstimatedDuration`.
   * Si la línea contenía `M0|M1|M30` y `PauseOnHold==true`, `State=Paused`.
   * Si `FilePosition<FileLength` y `State==Sending`, envía la siguiente línea.
   * Si `FilePosition==FileLength`, `State=Idle` (fin de archivo).
4. Al recibir `error:`:
   * Emite `ErrorOccurred` con mensaje, `State=Paused`.

## Reglas e Invariantes
* `Goto()`:
  * Permitido sólo si `State ∈ {Idle, Paused}`.
  * Rango: clamped a `[0, FileLength]`.
* `Start()` en `Sending` no debe duplicar envíos.
* `Clear()` siempre lleva a `Idle`, resetea `FileLength=0`, `FilePosition=0`.
* Eventos pueden emitirse desde hilos de recepción; la implementación garantiza sincronización mínima o la VM se encarga de marshalling al hilo de UI.

## Estimación de Duración (MVP)
* Heurística simple basada en ritmo medio por línea: `Estimated = avgLineTime * FileLength`.
* Mejoras futuras: feedrate efectivo, longitud de trayectorias, dwell (`G4`), aceleración del planner.

## Errores y Recuperación
* En `error:` → `Paused` y notificación; opciones: `Goto()` para reintentar/reanudar o `Clear()`.
* Si el puerto se desconecta en `Sending`, transición a `Paused` y notificar.

## Casos Borde
* `Start()` sin líneas o sin puerto abierto: no hace nada o lanza excepción controlada (según política).
* Líneas vacías/comentarios: deben avanzar (`ok`) sin efectos.
* `PauseOnHold`: sólo se evalúa tras `ok` de la línea con el M‑code.

## Validación
* Tests de integración cubren: `Start→ok→finish`, `Pause`, `PauseOnHold (M0/M1/M30)`, `error:` y `Goto`.
