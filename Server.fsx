open System
open System.Net
open System.Net.Sockets
open System.IO
open System.Threading

exception Terminate of string

let mutable clientConnections = [||]

// Function to trim trailing nulls from a string
let trimStringEnd (streamInput: string) =
    try
        let endIdx =
            streamInput
            |> Seq.rev
            |> Seq.findIndex (fun char -> char <> '\000')
        streamInput.[0..(streamInput.Length - endIdx - 1)]
    with
    | :? System.Collections.Generic.KeyNotFoundException -> ""


// Function to send a message to the client's stream
let sendToClient(stream: NetworkStream, message: string) = 
    let clientmessagetosent = System.Text.Encoding.ASCII.GetBytes(message)
    stream.Write(clientmessagetosent)


// Function to parse an integer from an input operand
let parseIntFromInput(value: string): int =
    try
        let intValue = Int32.Parse(value)
        intValue
    with
    | :? System.FormatException ->
        -4

// Function to calculate the result of an operation with operands
let calculate(command: string, values: int array) : int =
    match command, values.Length with
    | "add", 4 -> values.[0] + values.[1] + values.[2] + values.[3]
    | "add", 3 -> values.[0] + values.[1] + values.[2]
    | "add", _ -> values.[0] + values.[1]
    
    | "subtract", 4 -> values.[0] - values.[1] - values.[2] - values.[3]
    | "subtract", 3 -> values.[0] - values.[1] - values.[2]
    | "subtract", _ -> values.[0] - values.[1]

    | _, 4 -> values.[0] * values.[1] * values.[2] * values.[3]
    | _, 3 -> values.[0] * values.[1] * values.[2]
    | _, _ -> values.[0] * values.[1]

// Function to evaluate an expression from the client's input
let evaluate(value: string) : string =
    let chunks = value.Split(' ')
    let integerArray = Array.create (chunks.Length - 1) 0
    for i = 1 to chunks.Length - 1 do
        integerArray.[i - 1] <- parseIntFromInput chunks.[i]
    if Array.contains -4 integerArray then
        string -4
    else
        let result = calculate(chunks.[0], integerArray)
        if result < 0 then
            string -1
        else
            string result

// Function to validate and process client input
let validateAndProcess(input: string): string =
    let Operators = ["add"; "subtract"; "multiply"]
    let inputParts: string array = input.Trim(' ').ToString().ToLower().Split(' ')
    let inputWithoutNulls = trimStringEnd input
    if inputWithoutNulls = "bye" then
        "-5"
    elif inputWithoutNulls = "terminate" then
        "-10"
    elif not (List.contains inputParts.[0] Operators) then
        "-1"
    elif inputParts.Length < 3 then
        "-2"
    elif inputParts.Length > 5 then
        "-3"
    else
        evaluate input

// Function to serve a client
let Clientservice (client: TcpClient, clientNumber: int) =  
    try
        let stream = client.GetStream()
        let message = "Hello\n"
        sendToClient(stream, message)
        let mutable isOpen = true
        while isOpen && client.Connected do
            let buffer = Array.zeroCreate 256
            let read = stream.Read(buffer, 0, 256)
            let input = System.Text.Encoding.ASCII.GetString(buffer)
            printfn "Received: %s" input
            let answer = validateAndProcess input
            if answer = "-10" then
                raise (Terminate("-10"))
            printfn "Responding to client %d with result: %s" clientNumber answer
            sendToClient(stream, answer)
            if answer = "-5" then
                stream.Flush()
                isOpen <- false
                client.Close()
    with
    | :? SocketException as ex ->
        client.Close()
    | :? IOException as ex ->
        client.Close()
    | :? Terminate -> reraise()
    | ex ->
        printfn "An unhandled exception occurred: %s, Closing client %d" ex.Message clientNumber
let ipAddress = IPAddress.Parse("127.0.0.1")
let mutable isServerUp = true

// Retrieve the port number from command-line arguments
let args = System.Environment.GetCommandLineArgs()
let mutable port = 9009 // Default port number
if args.Length >=3 then
    let success, parsedPort = Int32.TryParse(args.[2])
    if success then
        port <- parsedPort

let listener: TcpListener = new TcpListener(ipAddress, port)
listener.Start()
printfn "Server is running and listening on port %d" port

// Function to terminate the server
let terminateServer() = 
    // Internal function to gracefully disconnect a client
    let disconnectClient (client: TcpClient) =
        if client.Connected then
            let stream = client.GetStream()
            sendToClient(stream, "-5")
            stream.Flush()
            client.Close()

    // Disconnect all clients
    Array.iter disconnectClient clientConnections
    
    // Stop listening for new clients
    try
        listener.Stop()
    with
    | _ -> ()  // Ignoring potential exceptions when stopping the listener
    
    isServerUp <- false
    printfn "Server terminated."


// Function to handle client connections
let rec handleClients() = async {
    let client = listener.AcceptTcpClient()
    async {
        try
            let newArraySize = clientConnections.Length + 1
            let newArray: TcpClient array = Array.zeroCreate<TcpClient> newArraySize
            clientConnections.CopyTo(newArray, 0)
            newArray.[newArraySize - 1] <- client
            clientConnections <- newArray
            Clientservice(client, clientConnections.Length)
        with
        | :? Terminate -> terminateServer()
    }|> Async.Start
    return! handleClients()
}

let clientsHandler: obj = 
    try
        handleClients () |> Async.RunSynchronously 
    with
    | :? SocketException as ex ->
        0