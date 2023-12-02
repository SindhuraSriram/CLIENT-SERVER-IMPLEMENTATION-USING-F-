open System
open System.Net.Sockets
open System.Threading

// Define a function to map error codes to error messages
let mapErrorCode (code: string): string =
    match code with
    | "-1" -> "Incorrect operation command."
    | "-2" -> "Number of inputs is less than two."
    | "-3" -> "Number of inputs is more than four."
    | "-4" -> "One or more of the inputs contain(s) non-number(s)."
    | "-5" -> "Exit."
    | errorCode when errorCode.StartsWith("-") -> "Unknown error code: " + errorCode
    | validResult -> validResult


try
    // Server details
    let serverAddress = "127.0.0.1"
    let args = System.Environment.GetCommandLineArgs()
    let mutable serverPort = 9009 // Default port number
    if args.Length >=3  then
        let success, parsedPort = Int32.TryParse(args.[1])
        if success then
            serverPort <- parsedPort

    // Connect to the server
    let client = new TcpClient(serverAddress, serverPort)
    let mutable isClientConnected = true
    let stream = client.GetStream()

    // Read the initial response from the server
    let responseBuffer = Array.zeroCreate 256
    stream.Read(responseBuffer, 0, 256) |> ignore
    let serverResponse = System.Text.Encoding.ASCII.GetString(responseBuffer)
    printf "Server Response: %sSending Command: " serverResponse

    // Function to trim trailing null characters from a string
    let trimStringEnd (streamInput: string) =
        try
            let endIdx =
                streamInput
                |> Seq.rev
                |> Seq.findIndex (fun char -> char <> '\000')
            streamInput.[0..(streamInput.Length - endIdx - 1)]
        with
        | :? System.Collections.Generic.KeyNotFoundException -> ""

    // Function to listen to user input
    let listenToUser() = 
        while isClientConnected do
            let userInput = Console.ReadLine()
            stream.Write(System.Text.Encoding.ASCII.GetBytes(userInput))

    // Start a thread to listen to user input
    let userListenerThread = new Thread(new ThreadStart(listenToUser))
    userListenerThread.IsBackground <- true
    userListenerThread.Start() 

    // Main loop to handle server responses
    while isClientConnected do 
        // Read the server's response
        let responseBuffer = Array.zeroCreate 256
        stream.Read(responseBuffer, 0, 256) |> ignore
        let serverResponse = System.Text.Encoding.ASCII.GetString(responseBuffer)
        let responseWithoutNulls = trimStringEnd serverResponse
        let errorMessage = mapErrorCode responseWithoutNulls

        if responseWithoutNulls = "-5" then
            // If the server indicates an exit, close the client connection
            isClientConnected <- false
            stream.Close()
        
        
        if errorMessage <> "Exit." then
            printfn "Server Response: %s" errorMessage
            printf "Sending Command: "
        else
            printfn "exit"
    
    // Close the client connection when done
    client.Close()
with
    | :? SocketException as socketException ->
        // Handle socket-related exceptions
        printfn "Error while connecting to server: %s" socketException.Message