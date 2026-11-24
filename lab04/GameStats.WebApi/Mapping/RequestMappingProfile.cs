using AutoMapper;
using GameStats.Application.Events.Commands.CreateGameEvent;
using GameStats.Application.Runs.Commands.CreateRun;
using GameStats.WebApi.Models;

namespace GameStats.WebApi.Mapping;

public class RequestMappingProfile : Profile
{
    public RequestMappingProfile()
    {
        CreateMap<CreateRunRequest, CreateRunCommand>();
        CreateMap<CreateGameEventRequest, CreateGameEventCommand>();
    }
}