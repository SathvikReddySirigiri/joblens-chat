namespace JobLens.Application.Dtos;

public sealed record CaseStudy(
    string Id,
    string Title,
    string ScamType,
    string Summary,
    IReadOnlyList<CaseEmail> Emails,
    IReadOnlyList<string> RedFlags,
    IReadOnlyList<string> WhatToDo,
    string RecruiterEmail,
    string ChatQuestion,
    string OfferText);

public sealed record CaseEmail(
    string Step,
    string ImageFile,
    string FromName,
    string FromEmail,
    string To,
    string Date,
    string Subject,
    string Body,
    bool IsStudentReply);
