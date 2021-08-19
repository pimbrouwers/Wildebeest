namespace Todo.Common

/// Internal URLs
module Urls = 
    let ``/`` = "/"
    let ``/todo/create`` = "/todo/create"
    let ``/todo/complete/{id}`` = sprintf "/todo/change-status?id=%s&complete=true"
    let ``/todo/incomplete/{id}`` = sprintf "/todo/change-status?id=%s&complete=false"

